using Aimmy2.AILogic;
using Aimmy2.Class;
using Newtonsoft.Json;
using Other;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Aimmy2.Controls;

public partial class ModelDashboard : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private string _details = "";
    private CancellationTokenSource? _measurement;
    public ModelDashboard()
    {
        InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) =>
        {
            _timer.Start(); Refresh();
        };
        Unloaded += (_, _) => { _timer.Stop(); _measurement?.Cancel(); };
    }
    private void Refresh()
    {
        try
        {
            var manager = FileManager.AIManager;
            SlotBadge.Text = $"SLOT {AIManager.ActiveSlot}";
            ModelMetadata? metadata = null;
            bool available = manager == null || manager.TryGetModelMetadata(AIManager.ActiveSlot, out metadata);
            if (available)
            {
                ModelTitle.Text = metadata == null ? "Chưa tải model" : Path.GetFileName(metadata.ModelPath);
                ModelTitle.ToolTip = metadata?.ModelPath;
                ModelSummary.Text = "Chọn model ở thẻ Slot 1 hoặc Slot 2 để bắt đầu.";
                if (metadata != null)
                {
                    var size = metadata.ResolveSize(manager!.GetSlotImageSize(AIManager.ActiveSlot));
                    string precision = metadata.Input.DataType is "kHALF" or "Float16" ? "FP16" : "FP32";
                    ModelSummary.Text = $"{metadata.Backend}  ·  I/O {precision}  ·  {size.Width} × {size.Height}  ·  {(metadata.Dynamic ? "Động" : "Cố định")}";
                }
                string details = metadata == null ? "Chưa có model." : JsonConvert.SerializeObject(metadata, Formatting.Indented);
                if (details != _details) { TechnicalDetails.Text = details; _details = details; }
            }
            CaptureSummary.Text = $"{Dictionary.dropdownState["Screen Capture Method"]}  ·  Capture {manager?.IMAGE_SIZE ?? 0} px  ·  Aim Assist {((bool)Dictionary.toggleState["Aim Assist"] ? "bật" : "tắt")}";
            var perf = manager?.GetPerformanceSnapshot();
            string Metric(string name) => perf != null && perf.TryGetValue(name, out double ms) ? $"{ms:F1} ms" : "—";
            CaptureMetric.Text = Metric("ScreenGrab");
            InferenceMetric.Text = Metric("ModelInference");
            FpsMetric.Text = perf != null && perf.TryGetValue("InferenceFPS", out double fps) ? $"{fps:F0}" : "—";

        }
        catch (Exception ex) { StatusText.Text = "Chưa đọc được trạng thái model. " + ex.Message; }
    }
    private void CancelMeasurement(object sender, RoutedEventArgs e) => _measurement?.Cancel();
    private async void OpenPerformanceHelper(object sender, RoutedEventArgs e)
    {
        if (_measurement != null) return;
        if (FileManager.AIManager is not { } manager) { StatusText.Text = "Hãy tải model trước."; return; }
        using var cancellation = new CancellationTokenSource();
        _measurement = cancellation;
        MeasureButton.IsEnabled = false;
        CancelMeasureButton.Visibility = MeasureProgress.Visibility = Visibility.Visible;
        MeasureProgress.Value = 0;
        StatusText.Text = "";
        PerformanceSummary.Text = "Đang đo… Tiếp tục nhận diện bằng phím kích hoạt của bạn.";
        try
        {
            var sample = await manager.MeasureLiveAsync(new Progress<double>(p => MeasureProgress.Value = p), cancellation.Token);
            PerformanceSummary.Text = sample.Frames == 0
                ? "Chưa có lượt suy luận trong 5 giây. Kiểm tra model, bật tính năng nhận diện và giữ phím kích hoạt rồi đo lại."
                : $"{sample.Fps:F1} FPS · {sample.Frames} lượt / {sample.Seconds:F1} giây\nSuy luận {sample.InferenceMs:F2} ms · Capture {sample.CaptureMs:F2} ms";
        }
        catch (OperationCanceledException) { PerformanceSummary.Text = "Đã hủy phép đo."; }
        catch (Exception ex) { PerformanceSummary.Text = ex.Message; }
        finally
        {
            _measurement = null;
            MeasureButton.IsEnabled = true;
            CancelMeasureButton.Visibility = MeasureProgress.Visibility = Visibility.Collapsed;
        }
    }
}
