using System.Drawing;

namespace Aimmy2.AILogic;

/// <summary>All capture and screen values are physical pixels, including negative desktop origins.</summary>
public sealed class CaptureTransform
{
    public Rectangle Capture { get; }
    public int CaptureLeft => Capture.Left;
    public int CaptureTop => Capture.Top;
    public int CaptureWidth => Capture.Width;
    public int CaptureHeight => Capture.Height;
    public int DisplayLeft { get; init; }
    public int DisplayTop { get; init; }
    public int ModelWidth { get; }
    public int ModelHeight { get; }
    public float ScaleX { get; }
    public float ScaleY { get; }
    public float UniformScale { get; }
    public float PadX { get; }
    public float PadY { get; }
    public double DpiScaleX { get; init; } = 1;
    public double DpiScaleY { get; init; } = 1;
    public int Rotation { get; init; }
    public CaptureTransform(Rectangle capture, int width, int height, bool letterbox)
    {
        if (capture.Width <= 0 || capture.Height <= 0 || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(capture));
        Capture = capture; ModelWidth = width; ModelHeight = height;
        UniformScale = Math.Min((float)width / capture.Width, (float)height / capture.Height);
        ScaleX = letterbox ? UniformScale : (float)width / capture.Width;
        ScaleY = letterbox ? UniformScale : (float)height / capture.Height;
        PadX = letterbox ? (width - capture.Width * UniformScale) / 2 : 0;
        PadY = letterbox ? (height - capture.Height * UniformScale) / 2 : 0;
    }
    public PointF ModelToCapture(float x, float y) => new((x - PadX) / ScaleX, (y - PadY) / ScaleY);
    public PointF ModelToScreen(float x, float y) { var p = ModelToCapture(x, y); return new(p.X + CaptureLeft, p.Y + CaptureTop); }
    public RectangleF ModelToCapture(RectangleF box)
    {
        var p = ModelToCapture(box.Left, box.Top);
        return new(p.X, p.Y, box.Width / ScaleX, box.Height / ScaleY);
    }
}

internal static class ModelPreprocessor
{
    // Source is RGB planar 0..1 from every capture backend. Bilinear stretch/letterbox,
    // channel order and normalization use the same transform later inverted by detection.
    internal static float[] Prepare(float[] source, CaptureTransform t, ModelMetadata model, ref float[]? buffer)
    {
        var o = model.Options;
        if (!model.Nhwc && !o.Bgr && !o.Letterbox && t.ModelWidth == t.CaptureWidth && t.ModelHeight == t.CaptureHeight && o.InputScale == 1f / 255f && o.InputOffset == 0) return source;
        int plane = checked(t.ModelWidth * t.ModelHeight), srcPlane = checked(t.CaptureWidth * t.CaptureHeight);
        if (source.Length != srcPlane * 3) throw new ArgumentException("Capture tensor length mismatch.");
        if (buffer?.Length != plane * 3) buffer = new float[checked(plane * 3)];
        for (int y = 0; y < t.ModelHeight; y++)
        for (int x = 0; x < t.ModelWidth; x++)
        {
            float sx = (x + .5f - t.PadX) / t.ScaleX - .5f, sy = (y + .5f - t.PadY) / t.ScaleY - .5f;
            bool padding = x + .5f < t.PadX || x + .5f >= t.ModelWidth - t.PadX || y + .5f < t.PadY || y + .5f >= t.ModelHeight - t.PadY;
            sx = Math.Clamp(sx, 0, t.CaptureWidth - 1); sy = Math.Clamp(sy, 0, t.CaptureHeight - 1);
            int x0 = (int)sx, y0 = (int)sy, x1 = Math.Min(x0 + 1, t.CaptureWidth - 1), y1 = Math.Min(y0 + 1, t.CaptureHeight - 1);
            float fx = sx - x0, fy = sy - y0;
            for (int c = 0; c < 3; c++)
            {
                int src = (o.Bgr ? 2 - c : c) * srcPlane;
                float a = source[src + y0 * t.CaptureWidth + x0] * (1 - fx) + source[src + y0 * t.CaptureWidth + x1] * fx;
                float b = source[src + y1 * t.CaptureWidth + x0] * (1 - fx) + source[src + y1 * t.CaptureWidth + x1] * fx;
                float value = padding ? 114f : (a * (1 - fy) + b * fy) * 255;
                buffer[model.Nhwc ? (y * t.ModelWidth + x) * 3 + c : c * plane + y * t.ModelWidth + x] = value * o.InputScale + o.InputOffset;
            }
        }
        return buffer;
    }
}
