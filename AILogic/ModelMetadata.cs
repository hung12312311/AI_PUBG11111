using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Aimmy2.AILogic;

public sealed record TensorMetadata(string Name, int[] Shape, string DataType);

/// <summary>Explicit overrides for semantics that tensor dimensions alone cannot prove.</summary>
public sealed class ModelOptions
{
    public string InputLayout { get; set; } = "Auto";
    public string OutputLayout { get; set; } = "Auto"; // BCN, BNC, EndToEnd, MultiTensor
    public string Postprocess { get; set; } = "Auto"; // Raw, BuiltInNms, NmsFree
    public bool Objectness { get; set; }
    public bool NormalizedBoxes { get; set; }
    public bool Letterbox { get; set; }
    public float InputScale { get; set; } = 1f / 255f;
    public float InputOffset { get; set; }
    public bool Bgr { get; set; }
    public int DynamicWidth { get; set; }
    public int DynamicHeight { get; set; }
    public int? Stride { get; set; }
    public int ClassCount { get; set; }
    public string BoxesName { get; set; } = "boxes";
    public string ScoresName { get; set; } = "scores";
    public string ClassesName { get; set; } = "classes";
    public string CountName { get; set; } = "num_detections";
    public static ModelOptions Load(string path) => File.Exists(path + ".aimmy.json")
        ? JsonConvert.DeserializeObject<ModelOptions>(File.ReadAllText(path + ".aimmy.json")) ?? new()
        : new();
}

public sealed class ModelMetadata
{
    public string Format { get; init; } = "ONNX";
    public string Backend { get; init; } = "Unknown";
    public string ModelPath { get; init; } = "";
    public TensorMetadata Input { get; init; } = new("", [], "");
    public IReadOnlyList<TensorMetadata> Outputs { get; init; } = [];
    public ModelOptions Options { get; init; } = new();
    public bool Nhwc { get; init; }
    public bool Dynamic => Input.Shape.Any(d => d <= 0);
    public int WidthAxis => Nhwc ? 2 : 3;
    public int HeightAxis => Nhwc ? 1 : 2;
    public int ClassCount { get; init; }
    public (int Width, int Height) ResolveSize(int preferred)
    {
        int w = Input.Shape[WidthAxis] > 0 ? Input.Shape[WidthAxis] : Options.DynamicWidth > 0 ? Options.DynamicWidth : preferred;
        int h = Input.Shape[HeightAxis] > 0 ? Input.Shape[HeightAxis] : Options.DynamicHeight > 0 ? Options.DynamicHeight : preferred;
        if (w <= 0 || h <= 0) throw new NotSupportedException("Input dimensions must be positive.");
        _ = checked(w * h * 3);
        if (Options.Stride is int stride && (stride <= 0 || w % stride != 0 || h % stride != 0))
            throw new NotSupportedException($"Input {w} × {h} does not satisfy the declared stride {stride}.");
        return (w, h);
    }

    internal static ModelMetadata Read(InferenceSession session, string path, string provider)
    {
        if (session.InputMetadata.Count != 1) throw new NotSupportedException("Only one image input is supported; extra inputs need an explicit adapter.");
        var input = session.InputMetadata.Single();
        var shape = input.Value.Dimensions;
        var options = ModelOptions.Load(path);
        if (shape.Length != 4 || shape[0] > 1) throw new NotSupportedException($"Expected batch-one rank-four image input, got [{string.Join(',', shape)}].");
        bool nhwc = options.InputLayout == "NHWC" || (options.InputLayout == "Auto" && shape[1] != 3 && shape[3] == 3);
        if (shape[nhwc ? 3 : 1] != 3) throw new NotSupportedException("RGB/BGR three-channel input layout is not established; specify InputLayout in .aimmy.json.");
        if (input.Value.ElementDataType is not (TensorElementType.Float or TensorElementType.Float16))
            throw new NotSupportedException($"Input dtype {input.Value.ElementDataType} needs a separate preprocessor.");
        int classes = options.ClassCount;
        var custom = session.ModelMetadata.CustomMetadataMap;
        if (options.Stride == null && custom.TryGetValue("stride", out var strideText) && int.TryParse(strideText, out var stride) && stride > 0)
            options.Stride = stride;
        if (classes == 0 && custom.TryGetValue("names", out var names))
        {
            try { var obj = JObject.Parse(names.Replace("'", "\"")); classes = obj.Properties().Max(p => int.Parse(p.Name)) + 1; }
            catch { /* Class labels are optional; output signatures or sidecar supply the count. */ }
        }
        if (options.Postprocess == "Auto" && custom.TryGetValue("end2end", out var end2end) && bool.TryParse(end2end, out var e2e) && e2e)
            options.Postprocess = "NmsFree";
        return new ModelMetadata { ModelPath = path, Backend = provider, Input = new(input.Key, shape, input.Value.ElementDataType.ToString()),
            Outputs = session.OutputMetadata.Select(x => new TensorMetadata(x.Key, x.Value.Dimensions, x.Value.ElementDataType.ToString())).ToArray(),
            Options = options, Nhwc = nhwc, ClassCount = classes };
    }
}
