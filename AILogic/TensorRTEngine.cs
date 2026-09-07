using System;
using System.IO;
using System.Linq;
using JYPPX.TensorRtSharp.Nvinfer;
using JYPPX.TensorRtSharp.Cuda;

namespace Aimmy2.AILogic
{
    public class TensorRTEngine : IDisposable
    {
        private Runtime? _runtime;
        private CudaEngine? _engine;
        private JYPPX.TensorRtSharp.Nvinfer.ExecutionContext? _context;
        private CudaStream? _stream;

        private Cuda1DMemory<float>? _deviceInput;
        private Cuda1DMemory<float>? _deviceOutput;
        private float[]? _outputHostBuffer;

        private int _inputSize;
        private int _outputSize;

        public string InputName { get; private set; } = "images";
        public string OutputName { get; private set; } = "output0";

        public int[] InputDims { get; private set; } = new int[] { 1, 3, 640, 640 };
        public int[] OutputDims { get; private set; } = new int[] { 1, 84, 8400 };

        public TensorRTEngine(string enginePath)
        {
            // 1. Initialize Runtime
            _runtime = new Runtime();

            // 2. Deserialize Engine
            byte[] engineData = File.ReadAllBytes(enginePath);
            _engine = _runtime.deserializeCudaEngineByBlob(engineData, (ulong)engineData.Length);
            if (_engine == null)
            {
                throw new Exception("Failed to deserialize TensorRT engine from: " + enginePath);
            }

            // 3. Create Execution Context
            _context = _engine.createExecutionContext();
            if (_context == null)
            {
                throw new Exception("Failed to create TensorRT execution context.");
            }

            _stream = new CudaStream();

            // 4. Query Inputs and Outputs details
            int numTensors = _engine.getNbIOTensors();
            for (int i = 0; i < numTensors; i++)
            {
                string name = _engine.getIOTensorName(i);
                Dims shape = _engine.getTensorShape(name);
                int[] dims = new int[shape.nbDims];
                int totalSize = 1;
                for (int d = 0; d < shape.nbDims; d++)
                {
                    dims[d] = (int)shape.d[d];
                    // If dynamic (-1), default to standard YOLO dimensions
                    if (dims[d] < 0)
                    {
                        if (d == 0) dims[d] = 1;
                        else if (d == 1) dims[d] = 3;
                        else dims[d] = 640;
                    }
                    totalSize *= dims[d];
                }

                // First tensor is usually the input, or we can check its name
                if (i == 0 || name.ToLower().Contains("images") || name.ToLower().Contains("input"))
                {
                    InputName = name;
                    InputDims = dims;
                    _inputSize = totalSize;
                }
                else
                {
                    OutputName = name;
                    OutputDims = dims;
                    _outputSize = totalSize;
                }
            }

            // 5. Allocate memory on GPU (VRAM)
            _deviceInput = new Cuda1DMemory<float>((ulong)_inputSize);
            _deviceOutput = new Cuda1DMemory<float>((ulong)_outputSize);

            // 6. Bind addresses
            _context.setInputTensorAddress(InputName, _deviceInput.get());
            _context.setOutputTensorAddress(OutputName, _deviceOutput.get());
            
            _outputHostBuffer = new float[_outputSize];
        }

        public float[] RunInference(float[] inputData)
        {
            if (_context == null || _stream == null || _deviceInput == null || _deviceOutput == null || _outputHostBuffer == null)
            {
                throw new ObjectDisposedException(nameof(TensorRTEngine));
            }

            // Copy input to GPU
            _deviceInput.copyFromHost(inputData);

            // Execute inference
            _context.executeV3(_stream);

            // Sync stream
            _stream.Synchronize();

            // Copy output from GPU
            _deviceOutput.copyToHost(_outputHostBuffer);

            return _outputHostBuffer;
        }

        public void Dispose()
        {
            _deviceInput?.Dispose();
            _deviceOutput?.Dispose();
            _stream?.Dispose();
            _context?.Dispose();
            _engine?.Dispose();
            _runtime?.Dispose();

            _deviceInput = null;
            _deviceOutput = null;
            _outputHostBuffer = null;
            _stream = null;
            _context = null;
            _engine = null;
            _runtime = null;
        }
    }
}
