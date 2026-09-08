using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Runtime.CompilerServices;

namespace Aimmy2.AILogic;

internal static class OnnxModelSessionFactory
{
    private sealed class State(ModelMetadata metadata)
    {
        internal ModelMetadata Metadata = metadata;
        internal Float16[]? HalfInput;
        internal float[]? Prepared;
    }
    private static readonly ConditionalWeakTable<InferenceSession, State> States = new();
    internal static ModelMetadata Metadata(InferenceSession session) => States.TryGetValue(session, out var state)
        ? state.Metadata : throw new InvalidOperationException("Session was not initialized by the model factory.");

    internal static InferenceSession Load(string path, string requested, int preferredSize = 640)
    {
        var available = OrtEnv.Instance().GetAvailableProviders();
        var candidates = requested == "Auto" ? new[] { "CUDA", "DirectML", "CPU" } : requested == "CPU" ? new[] { "CPU" } : new[] { requested, "CPU" };
        var failures = new List<string>();
        foreach (string provider in candidates)
        {
            string native = provider == "DirectML" ? "DmlExecutionProvider" : provider + "ExecutionProvider";
            if (!available.Contains(native)) { failures.Add(provider + ": provider is not installed in this runtime"); continue; }
            // Fresh options per attempt: a failed DML/CUDA provider must not survive into CPU fallback.
            using var options = new SessionOptions { EnableCpuMemArena = true, EnableMemoryPattern = false,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL, ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1, IntraOpNumThreads = Math.Max(1, Math.Min(4, Environment.ProcessorCount)) };
            InferenceSession? session = null;
            try
            {
                if (provider == "DirectML") options.AppendExecutionProvider_DML();
                else if (provider == "CUDA") options.AppendExecutionProvider_CUDA();
                else options.AppendExecutionProvider_CPU();
                session = new InferenceSession(path, options);
                var metadata = ModelMetadata.Read(session, path, provider);
                States.Add(session, new State(metadata));
                // Validate execution and output semantics before replacing a working session.
                var size = metadata.ResolveSize(preferredSize);
                var transform = new CaptureTransform(new System.Drawing.Rectangle(0, 0, size.Width, size.Height), size.Width, size.Height, false);
                using (var validationRun = new RunOptions())
                    Run(session, new float[checked(size.Width * size.Height * 3)], transform, validationRun, 1);
                if (failures.Count > 0) global::Other.LogManager.Log(global::Other.LogManager.LogLevel.Warning, $"Using {provider}. {string.Join("; ", failures)}");
                return session;
            }
            catch (Exception ex) { session?.Dispose(); failures.Add(provider + ": " + ex.Message); }
        }
        throw new NotSupportedException("Không tải được model trên backend khả dụng. " + string.Join("; ", failures));
    }

    internal static Tensor<float> Run(InferenceSession session, float[] capture, CaptureTransform transform, RunOptions runOptions, float confidence)
    {
        var state = States.GetValue(session, _ => throw new InvalidOperationException("Missing model metadata."));
        var metadata = state.Metadata;
        float[] input = ModelPreprocessor.Prepare(capture, transform, metadata, ref state.Prepared);
        int[] dimensions = metadata.Nhwc ? [1, transform.ModelHeight, transform.ModelWidth, 3] : [1, 3, transform.ModelHeight, transform.ModelWidth];
        NamedOnnxValue value;
        if (metadata.Input.DataType == "Float16")
        {
            if (state.HalfInput?.Length != input.Length) state.HalfInput = new Float16[input.Length];
            for (int i = 0; i < input.Length; i++) state.HalfInput[i] = (Float16)input[i];
            value = NamedOnnxValue.CreateFromTensor(metadata.Input.Name, new DenseTensor<Float16>(state.HalfInput, dimensions));
        }
        else value = NamedOnnxValue.CreateFromTensor(metadata.Input.Name, new DenseTensor<float>(input, dimensions));
        using var results = session.Run(new[] { value }, session.OutputMetadata.Keys.ToArray(), runOptions);
        var outputs = results.ToDictionary(x => x.Name, ToFloatTensor);
        return ModelOutputAdapter.Canonicalize(outputs, metadata, transform, confidence);
    }

    internal static Tensor<float> ToFloatTensor(DisposableNamedOnnxValue value)
    {
        if (value.AsTensor<float>() is { } fp32) return fp32;
        if (value.AsTensor<Float16>() is { } fp16) return new DenseTensor<float>(fp16.Select(x => (float)x).ToArray(), fp16.Dimensions.ToArray());
        if (value.AsTensor<long>() is { } i64) return new DenseTensor<float>(i64.Select(x => (float)x).ToArray(), i64.Dimensions.ToArray());
        if (value.AsTensor<int>() is { } i32) return new DenseTensor<float>(i32.Select(x => (float)x).ToArray(), i32.Dimensions.ToArray());
        throw new NotSupportedException($"Output {value.Name}: dtype is not supported.");
    }
}
