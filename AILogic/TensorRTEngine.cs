using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime.Tensors;
using JYPPX.TensorRtSharp.Nvinfer;
using JYPPX.TensorRtSharp.Cuda;

namespace Aimmy2.AILogic;

public sealed class TensorRTEngine : IDisposable
{
    private Runtime? _runtime;
    private CudaEngine? _engine;
    private JYPPX.TensorRtSharp.Nvinfer.ExecutionContext? _context;
    private CudaStream? _stream;
    private readonly Dictionary<string, Binding> _bindings = new();
    private float[]? _prepared;
    public string InputName { get; private set; } = "";
    public string OutputName { get; private set; } = "";
    public int[] InputDims { get; private set; } = [];
    public int[] OutputDims { get; private set; } = [];
    public ModelMetadata Metadata { get; private set; } = new();
    public int[]? ProfileMin { get; private set; }
    public int[]? ProfileMax { get; private set; }

    private sealed class Binding : IDisposable
    {
        public TrtDataType Type { get; }
        public int[] Shape { get; }
        public byte[] Host { get; }
        public Cuda1DMemory<byte> Device { get; }
        public float[] Values { get; }
        public Binding(int[] shape, TrtDataType type)
        {
            Shape = shape; Type = type;
            int count = 1;
            foreach (int d in shape) { if (d <= 0) throw new NotSupportedException("Unresolved/data-dependent TensorRT output shape requires an output allocator."); count = checked(count * d); }
            int bytes = type switch { TrtDataType.kHALF => 2, TrtDataType.kFLOAT or TrtDataType.kINT32 => 4, TrtDataType.kINT64 => 8, _ => throw new NotSupportedException($"Unsupported TensorRT I/O dtype: {type}") };
            Host = new byte[checked(count * bytes)]; Values = new float[count];
            Device = new Cuda1DMemory<byte>((ulong)Host.Length);
        }
        public void Upload(float[] input)
        {
            if (input.Length != Values.Length) throw new ArgumentException("TensorRT input length mismatch.");
            if (Type == TrtDataType.kFLOAT) input.AsSpan().CopyTo(MemoryMarshal.Cast<byte, float>(Host));
            else if (Type == TrtDataType.kHALF)
            {
                var halves = MemoryMarshal.Cast<byte, Half>(Host);
                for (int i = 0; i < input.Length; i++) halves[i] = (Half)input[i];
            }
            else throw new NotSupportedException("Image input must be FP16 or FP32.");
            Device.copyFromHost(Host);
        }
        public Tensor<float> Download()
        {
            Device.copyToHost(Host);
            if (Type == TrtDataType.kFLOAT) MemoryMarshal.Cast<byte, float>(Host).CopyTo(Values);
            else for (int i = 0; i < Values.Length; i++) Values[i] = Type switch
            {
                TrtDataType.kHALF => (float)MemoryMarshal.Cast<byte, Half>(Host)[i],
                TrtDataType.kINT32 => MemoryMarshal.Cast<byte, int>(Host)[i],
                TrtDataType.kINT64 => MemoryMarshal.Cast<byte, long>(Host)[i],
                _ => throw new NotSupportedException()
            };
            return new DenseTensor<float>(Values, Shape);
        }
        public void Dispose() => Device.Dispose();
    }

    private static int[] Dimensions(Dims dims) => Enumerable.Range(0, dims.nbDims).Select(i => checked((int)dims.d[i])).ToArray();
    public TensorRTEngine(string enginePath)
    {
        try
        {
            var options = ModelOptions.Load(enginePath);
            _runtime = new Runtime();
            byte[] data = File.ReadAllBytes(enginePath);
            _engine = _runtime.deserializeCudaEngineByBlob(data, (ulong)data.Length) ?? throw new NotSupportedException("Cannot deserialize this engine on the installed GPU/runtime.");
            _context = _engine.createExecutionContext() ?? throw new NotSupportedException("Cannot create TensorRT context.");
            _stream = new CudaStream();
            var names = Enumerable.Range(0, _engine.getNbIOTensors()).Select(_engine.getIOTensorName).ToArray();
            var inputs = names.Where(n => _engine.getTensorIOMode(n) == TrtTensorIOMode.kINPUT).ToArray();
            if (inputs.Length != 1) throw new NotSupportedException("TensorRT requires a single image input; shape/extra inputs need an adapter.");
            InputName = inputs[0];
            int[] declared = Dimensions(_engine.getTensorShape(InputName));
            if (declared.Length != 4 || declared[0] > 1 || declared[1] != 3) throw new NotSupportedException("TensorRT currently accepts batch-one NCHW three-channel engines.");
            InputDims = (int[])declared.Clone();
            if (declared.Any(d => d <= 0))
            {
                ProfileMin = Dimensions(_engine.getProfileShape(InputName, 0, TrtOptProfileSelector.kMIN));
                ProfileMax = Dimensions(_engine.getProfileShape(InputName, 0, TrtOptProfileSelector.kMAX));
                InputDims = Dimensions(_engine.getProfileShape(InputName, 0, TrtOptProfileSelector.kOPT));
                InputDims[0] = 1;
                if (options.DynamicWidth > 0) InputDims[3] = options.DynamicWidth;
                if (options.DynamicHeight > 0) InputDims[2] = options.DynamicHeight;
                for (int i = 0; i < InputDims.Length; i++) if (InputDims[i] < ProfileMin[i] || InputDims[i] > ProfileMax[i]) throw new NotSupportedException("Requested input is outside TensorRT profile 0.");
                _context.setinputShape(InputName, new Dims(InputDims));
            }
            if (_engine.getTensorDataType(InputName) is not (TrtDataType.kFLOAT or TrtDataType.kHALF)) throw new NotSupportedException("Engine image input must be FP16/FP32.");
            var outputs = new List<TensorMetadata>();
            foreach (string name in names)
            {
                if (_engine.getTensorLocation(name) != TrtTensorLocation.kDEVICE || _engine.getTensorFormat(name) != TrtTensorFormat.kLINEAR || _engine.isShapeInferenceIO(name))
                    throw new NotSupportedException($"Tensor {name} requires a non-linear/host/shape binding adapter.");
                var shape = Dimensions(_context.getTensorShape(name));
                var dtype = _engine.getTensorDataType(name);
                var binding = new Binding(shape, dtype);
                _bindings.Add(name, binding);
                _context.setTensorAddress(name, binding.Device.get());
                if (name != InputName)
                {
                    outputs.Add(new(name, shape, dtype.ToString()));
                    if (OutputName.Length == 0) { OutputName = name; OutputDims = shape; }
                }
            }
            if (outputs.Count == 0) throw new NotSupportedException("Engine has no outputs.");
            if (declared.Any(d => d <= 0)) { options.DynamicWidth = InputDims[3]; options.DynamicHeight = InputDims[2]; }
            Metadata = new ModelMetadata { Format = "TensorRT", Backend = "TensorRT", ModelPath = enginePath,
                Input = new(InputName, declared, _engine.getTensorDataType(InputName).ToString()), Outputs = outputs,
                Options = options, ClassCount = options.ClassCount };
        }
        catch { Dispose(); throw; }
    }

    public Tensor<float> RunDetections(float[] capture, Rectangle region, float confidence)
    {
        if (_context == null || _stream == null) throw new ObjectDisposedException(nameof(TensorRTEngine));
        var transform = new CaptureTransform(region, InputDims[3], InputDims[2], Metadata.Options.Letterbox);
        float[] input = ModelPreprocessor.Prepare(capture, transform, Metadata, ref _prepared);
        _bindings[InputName].Upload(input);
        _context.executeV3(_stream);
        _stream.Synchronize();
        var outputs = _bindings.Where(x => x.Key != InputName).ToDictionary(x => x.Key, x => x.Value.Download());
        return ModelOutputAdapter.Canonicalize(outputs, Metadata, transform, confidence);
    }

    public float[] RunInference(float[] input)
    {
        if (_context == null || _stream == null) throw new ObjectDisposedException(nameof(TensorRTEngine));
        _bindings[InputName].Upload(input); _context.executeV3(_stream); _stream.Synchronize();
        return _bindings[OutputName].Download().ToArray();
    }
    public void Dispose()
    {
        foreach (var binding in _bindings.Values) binding.Dispose(); _bindings.Clear();
        _stream?.Dispose(); _stream = null; _context?.Dispose(); _context = null;
        _engine?.Dispose(); _engine = null; _runtime?.Dispose(); _runtime = null;
    }
}
