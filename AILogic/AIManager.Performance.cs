using System.Diagnostics;

namespace Aimmy2.AILogic;

public partial class AIManager
{
    private int _measurementRunning;
    public sealed record LiveMeasurement(int Frames, double Seconds, double InferenceMs, double CaptureMs)
    {
        public double Fps => Frames / Seconds;
    }
    public long GetInferenceCount()
    {
        lock (_benchmarkLock) return _benchmarks.TryGetValue("ModelInference", out var data) ? data.CallCount : 0;
    }
    private Dictionary<string, (long Count, double Total)> ReadCounters()
    {
        lock (_benchmarkLock) return _benchmarks.ToDictionary(x => x.Key, x => ((long)x.Value.CallCount, x.Value.TotalTime));
    }

    // Observe the existing pipeline. Never create another session, pause AI or change input actions.
    public async Task<LiveMeasurement> MeasureLiveAsync(IProgress<double>? progress, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _measurementRunning, 1, 0) != 0)
            throw new InvalidOperationException("Một phép đo đang chạy.");
        try
        {
            if (!TryGetModelMetadata(ActiveSlot, out var model) || model == null)
                throw new InvalidOperationException("Hãy chờ model tải xong rồi đo lại.");
            int slot = ActiveSlot;
            var before = ReadCounters();
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed.TotalSeconds < 5)
            {
                await Task.Delay(100, token);
                if (ActiveSlot != slot || (TryGetModelMetadata(slot, out var current) && !ReferenceEquals(current, model)))
                    throw new InvalidOperationException("Model đã đổi trong lúc đo. Hãy đo lại.");
                progress?.Report(Math.Min(1, timer.Elapsed.TotalSeconds / 5));
            }
            var after = ReadCounters();
            double Average(string name)
            {
                var a = after.GetValueOrDefault(name); var b = before.GetValueOrDefault(name);
                return a.Count > b.Count ? (a.Total - b.Total) / (a.Count - b.Count) : 0;
            }
            int frames = checked((int)(after.GetValueOrDefault("ModelInference").Count - before.GetValueOrDefault("ModelInference").Count));
            return new(frames, timer.Elapsed.TotalSeconds, Average("ModelInference"), Average("ScreenGrab"));
        }
        finally { Interlocked.Exchange(ref _measurementRunning, 0); }
    }

    internal async Task<PerformanceBenchmarkReport> RunPerformanceBenchmarkAsync(IProgress<PerformanceBenchmarkProgress>? progress,
        CancellationToken token, PerformanceGoal goal = PerformanceGoal.Balanced)
    {
        int size = GetSlotImageSize(ActiveSlot);
        var sample = await MeasureLiveAsync(new Progress<double>(p => progress?.Report(new(1, 1, size, "Đang đo phiên nhận diện hiện tại", p))), token);
        if (sample.Frames == 0) throw new InvalidOperationException("Chưa có lượt nhận diện. Bật tính năng nhận diện và giữ phím kích hoạt rồi đo lại.");
        var result = new PerformanceBenchmarkSizeResult(size, sample.Fps, sample.Fps, 0, 0, 0, 0, false, sample.Frames);
        var results = new[] { result };
        var choices = PerformanceRecommendationBuilder.BuildChoices(results, size, true, goal);
        return new(results, choices.Primary, true, choices, goal);
    }
}
