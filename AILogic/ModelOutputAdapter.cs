using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;

namespace Aimmy2.AILogic;

internal static class ModelOutputAdapter
{
    private sealed record Detection(RectangleF Box, float Score, int Class);

    // Canonical output is always [1,N,6], xyxy in capture pixels, postprocessed once.
    internal static Tensor<float> Canonicalize(IReadOnlyDictionary<string, Tensor<float>> outputs, ModelMetadata model, CaptureTransform t, float confidence)
    {
        var o = model.Options;
        var detections = new List<Detection>();
        bool raw = o.Postprocess == "Raw";
        void Add(float x1, float y1, float x2, float y2, float score, float classId)
        {
            if (!float.IsFinite(x1) || !float.IsFinite(y1) || !float.IsFinite(x2) || !float.IsFinite(y2) || !float.IsFinite(score) || !float.IsFinite(classId)
                || score < confidence || score > 1 || classId < 0 || classId != MathF.Truncate(classId) || classId > int.MaxValue) return;
            if (o.NormalizedBoxes) { x1 *= t.ModelWidth; x2 *= t.ModelWidth; y1 *= t.ModelHeight; y2 *= t.ModelHeight; }
            var box = t.ModelToCapture(new RectangleF(x1, y1, x2 - x1, y2 - y1));
            box = RectangleF.Intersect(box, new RectangleF(0, 0, t.CaptureWidth, t.CaptureHeight));
            if (box.Width > 0 && box.Height > 0) detections.Add(new(box, score, (int)classId));
        }
        if (outputs.Count > 1)
        {
            if (!outputs.TryGetValue(o.BoxesName, out var boxes) || !outputs.TryGetValue(o.ScoresName, out var scores) || !outputs.TryGetValue(o.ClassesName, out var classes))
                throw new NotSupportedException("Multiple outputs require boxes/scores/classes names (configure .aimmy.json for custom names).");
            if (boxes.Dimensions[^1] != 4) throw new NotSupportedException("Boxes output must end in four xyxy coordinates.");
            int n = checked((int)boxes.Length / 4);
            if (scores.Length != n || classes.Length != n) throw new NotSupportedException("Expected one score and class per box.");
            var b = boxes.ToArray(); var s = scores.ToArray(); var c = classes.ToArray();
            if (outputs.TryGetValue(o.CountName, out var count))
            {
                var countValue = count.First();
                if (!float.IsFinite(countValue) || countValue < 0 || countValue > n || countValue != MathF.Truncate(countValue)) throw new NotSupportedException("Invalid detection count.");
                n = (int)countValue;
            }
            for (int i = 0; i < n; i++) Add(b[i * 4], b[i * 4 + 1], b[i * 4 + 2], b[i * 4 + 3], s[i], c[i]);
        }
        else
        {
            var tensor = outputs.Single().Value;
            int[] d = tensor.Dimensions.ToArray();
            if (d.Length == 3 && d[0] != 1 || d.Length is not (2 or 3)) throw new NotSupportedException($"Unsupported output [{string.Join(',', d)}].");
            int a = d[^2], b = d[^1];
            var data = tensor.ToArray();
            int expectedChannels = model.ClassCount > 0 ? model.ClassCount + (o.Objectness ? 5 : 4) : 0;
            bool explicitEnd = o.OutputLayout == "EndToEnd" || o.Postprocess is "BuiltInNms" or "NmsFree";
            bool end = explicitEnd || (o.OutputLayout == "Auto" && !raw && b == 6 && expectedChannels != 6);
            if (end)
            {
                if (b != 6) throw new NotSupportedException("End-to-end detections require [N,6] or [1,N,6].");
                for (int i = 0; i < a; i++) Add(data[i * b], data[i * b + 1], data[i * b + 2], data[i * b + 3], data[i * b + 4], data[i * b + 5]);
            }
            else
            {
                bool bcn;
                if (o.OutputLayout == "BCN") bcn = true;
                else if (o.OutputLayout == "BNC") bcn = false;
                else if (expectedChannels > 0 && a == expectedChannels && b != expectedChannels) bcn = true;
                else if (expectedChannels > 0 && b == expectedChannels && a != expectedChannels && b != 6) bcn = false;
                else if (a >= 5 && a < b && b != 6) bcn = true;
                else throw new NotSupportedException($"Ambiguous output [{string.Join(',', d)}]. Specify OutputLayout (BCN/BNC/EndToEnd) and Postprocess in .aimmy.json.");
                int channels = bcn ? a : b, n = bcn ? b : a, offset = o.Objectness ? 5 : 4;
                int nc = channels - offset;
                if (nc < 1 || expectedChannels > 0 && channels != expectedChannels) throw new NotSupportedException("Class count does not match the output signature.");
                float At(int i, int c) => data[bcn ? c * n + i : i * channels + c];
                for (int i = 0; i < n; i++)
                {
                    float score = 0; int cls = 0;
                    for (int c = 0; c < nc; c++) if (At(i, offset + c) > score) { score = At(i, offset + c); cls = c; }
                    if (o.Objectness) score *= At(i, 4);
                    float x = At(i, 0), y = At(i, 1), w = At(i, 2), h = At(i, 3);
                    Add(x - w / 2, y - h / 2, x + w / 2, y + h / 2, score, cls);
                }
                raw = true;
            }
        }
        if (raw)
        {
            var ordered = detections.OrderByDescending(x => x.Score).ToList();
            detections = [];
            foreach (var candidate in ordered)
            {
                if (!detections.Any(kept => kept.Class == candidate.Class && IoU(kept.Box, candidate.Box) > .45f)) detections.Add(candidate);
            }
        }
        float[] canonical = new float[checked(detections.Count * 6)];
        for (int i = 0; i < detections.Count; i++)
        {
            var p = detections[i]; int k = i * 6;
            canonical[k] = p.Box.Left; canonical[k + 1] = p.Box.Top; canonical[k + 2] = p.Box.Right; canonical[k + 3] = p.Box.Bottom; canonical[k + 4] = p.Score; canonical[k + 5] = p.Class;
        }
        return new DenseTensor<float>(canonical, [1, detections.Count, 6]);
    }
    private static float IoU(RectangleF a, RectangleF b)
    {
        var r = RectangleF.Intersect(a, b);
        float area = Math.Max(0, r.Width) * Math.Max(0, r.Height);
        float union = a.Width * a.Height + b.Width * b.Height - area;
        return union > 0 ? area / union : 0;
    }
}
