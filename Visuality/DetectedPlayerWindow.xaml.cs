using Aimmy2.Class;
using Aimmy2.Theme;
using Class;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Visuality
{
    public partial class DetectedPlayerWindow : Window
    {
        // Windows API for forcing window position
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private bool _isInitialized = false;

        public DetectedPlayerWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
            _performanceTimer.Tick += (_, _) => RefreshPerformanceOverlay();
            IsVisibleChanged += (_, _) => { if (IsVisible) _performanceTimer.Start(); else _performanceTimer.Stop(); };
            Closed += (_, _) => _performanceTimer.Stop();

            //Subscribe to my Onlyfans to exclude bad Behavior!
            ThemeManager.ExcludeWindowFromBackground(this);

            Title = "";

            // Subscribe to display changes early
            DisplayManager.DisplayChanged += OnDisplayChanged;

            // Subscribe to property changes
            PropertyChanger.ReceiveDPColor = UpdateDPColor;
            PropertyChanger.ReceiveDPFontSize = UpdateDPFontSize;
            PropertyChanger.ReceiveDPWCornerRadius = ChangeCornerRadius;
            PropertyChanger.ReceiveDPWBorderThickness = ChangeBorderThickness;
            PropertyChanger.ReceiveDPWOpacity = ChangeOpacity;

            // Load initial values from Dictionary
            _fontSize = (int)Dictionary.sliderSettings["AI Confidence Font Size"];
            _cornerRadius = (int)Dictionary.sliderSettings["Corner Radius"];
            _borderThickness = Dictionary.sliderSettings["Border Thickness"];
            _boxOpacity = Dictionary.sliderSettings["Opacity"];
        }

        private readonly System.Windows.Threading.DispatcherTimer _performanceTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        public void RefreshPerformanceOverlay()
        {
            bool enabled = Dictionary.toggleState["Show Detected Player"] && Dictionary.toggleState["Show Detection Performance"];
            PerformanceBadge.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            var manager = Other.FileManager.AIManager;
            var stats = manager?.GetPerformanceSnapshot();
            double fps = stats != null && stats.TryGetValue("InferenceFPS", out double liveFps) ? liveFps : 0;
            string inference = stats != null && stats.TryGetValue("ModelInference", out double ms) ? $"{ms:F1} ms" : "—";
            string capture = stats != null && stats.TryGetValue("ScreenGrab", out double captureMs) ? $"{captureMs:F1} ms" : "—";
            CapturePerformance.Text = capture;
            InferencePerformance.Text = inference;
            FpsPerformance.Text = $"{fps:F0}";
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Make window click-through
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);

            // Now that we have a window handle, position the window
            if (!_isInitialized)
            {
                _isInitialized = true;
                ForceReposition();
            }
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {

            // Update position when display changes
            Application.Current.Dispatcher.Invoke(() =>
            {
                ForceReposition();
            });
        }

        public void ForceReposition()
        {
            try
            {

                // Get window handle
                var hwnd = _isInitialized ? new WindowInteropHelper(this).Handle : IntPtr.Zero;

                // Set window state to normal first
                this.WindowState = WindowState.Normal;

                // Position window to cover the current display (accounting for DPI scaling)
                this.Left = DisplayManager.ScreenLeft / WinAPICaller.scalingFactorX;
                this.Top = DisplayManager.ScreenTop / WinAPICaller.scalingFactorY;
                this.Width = DisplayManager.ScreenWidth / WinAPICaller.scalingFactorX;
                this.Height = DisplayManager.ScreenHeight / WinAPICaller.scalingFactorY;

                // Force position with Windows API if we have a handle
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, IntPtr.Zero,
                        DisplayManager.ScreenLeft,
                        DisplayManager.ScreenTop,
                        DisplayManager.ScreenWidth,
                        DisplayManager.ScreenHeight,
                        SWP_NOZORDER | SWP_NOACTIVATE);
                }

                // Maximize to cover entire display
                // Keep Normal: explicit monitor bounds provide full-screen coverage without activation.

                // Update tracer start position (changed to be dynamic)
                DetectedTracers.X1 = (DisplayManager.ScreenWidth / 2.0) / WinAPICaller.scalingFactorX;

                string tracerPosition = "Bottom"; // default value
                if (Dictionary.dropdownState.TryGetValue("Tracer Position", out var position))
                {
                    tracerPosition = position.ToString();
                }

                switch (tracerPosition)
                {
                    case "Bottom":
                        DetectedTracers.Y1 = DisplayManager.ScreenHeight / WinAPICaller.scalingFactorY;
                        break;
                    case "Middle":
                        DetectedTracers.Y1 = (DisplayManager.ScreenHeight / 2.0) / WinAPICaller.scalingFactorY;
                        break;
                    case "Top":
                        DetectedTracers.Y1 = 0;
                        break;
                }

                // Force layout update
                this.UpdateLayout();

            }
            catch (Exception ex)
            {
            }
        }

        private void UpdateDPColor(Color NewColor)
        {
            DetectedPlayerFocus.BorderBrush = new SolidColorBrush(NewColor);
            DetectedPlayerConfidence.Foreground = new SolidColorBrush(NewColor);
            DetectedTracers.Stroke = new SolidColorBrush(NewColor);
        }

        // Store UI settings for DrawAllDetections
        private int _fontSize = 20;
        private int _cornerRadius = 1;
        private double _borderThickness = 1;
        private double _boxOpacity = 1;

        private void UpdateDPFontSize(int newint)
        {
            _fontSize = newint;
            DetectedPlayerConfidence.FontSize = newint;
        }

        private void ChangeCornerRadius(int newint)
        {
            _cornerRadius = newint;
            DetectedPlayerFocus.CornerRadius = new CornerRadius(newint);
        }

        private void ChangeBorderThickness(double newdouble)
        {
            _borderThickness = newdouble;
            DetectedPlayerFocus.BorderThickness = new Thickness(newdouble);
            DetectedTracers.StrokeThickness = newdouble;
        }

        private void ChangeOpacity(double newdouble)
        {
            _boxOpacity = newdouble;
            DetectedPlayerFocus.Opacity = newdouble;
        }

        // Color palette for different classes
        private readonly Dictionary<string, Color> _classColors = new Dictionary<string, Color>
        {
            { "head", Color.FromRgb(255, 0, 0) },      // Red
            { "dau", Color.FromRgb(255, 0, 0) },       // Red (Vietnamese)
            { "body", Color.FromRgb(0, 255, 0) },      // Green
            { "than", Color.FromRgb(0, 255, 0) },      // Green (Vietnamese)
            { "enemy", Color.FromRgb(0, 255, 255) },   // Cyan
            { "default", Color.FromRgb(255, 255, 0) }  // Yellow
        };


        private Color GetColorFromState(string key, Color defaultColor)
        {
            if (Dictionary.colorState.TryGetValue(key, out var val) && val != null)
            {
                try
                {
                    return (Color)ColorConverter.ConvertFromString(val.ToString());
                }
                catch
                {
                }
            }
            return defaultColor;
        }

        public void DrawAllDetections(
            List<(string ClassName, double X, double Y, double Width, double Height, double Confidence)> detections,
            bool showConfidence = false,
            bool isEngine = false,
            bool isSingleClass = false)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Clear previous boxes
                DetectionCanvas.Children.Clear();

                foreach (var detection in detections)
                {
                    // Determine color based on model type (ONNX vs Engine) and class count
                    Color boxColor;
                    string classLower = detection.ClassName?.ToLower() ?? "";
                    
                    if (isSingleClass)
                    {
                        // 1 class (bất kể ONNX hay Engine) sẽ là màu xanh dương mặc định
                        boxColor = GetColorFromState("Single Class Color", Color.FromRgb(0, 120, 255));
                    }
                    else
                    {
                        if (isEngine)
                        {
                            // Engine 2 class: Body màu vàng, Head màu xanh dương mặc định
                            if (classLower.Contains("head") || classLower.Contains("dau") || classLower.Contains("đầu") || classLower.Contains("sọ"))
                            {
                                boxColor = GetColorFromState("Engine Head Color", Color.FromRgb(0, 120, 255)); // Head
                            }
                            else
                            {
                                boxColor = GetColorFromState("Engine Body Color", Color.FromRgb(255, 255, 0)); // Body / default
                            }
                        }
                        else
                        {
                            // ONNX 2 class: Body màu xanh lá cây, Head màu đỏ mặc định
                            if (classLower.Contains("head") || classLower.Contains("dau") || classLower.Contains("đầu") || classLower.Contains("sọ"))
                            {
                                boxColor = GetColorFromState("ONNX Head Color", Color.FromRgb(255, 0, 0)); // Head
                            }
                            else
                            {
                                boxColor = GetColorFromState("ONNX Body Color", Color.FromRgb(0, 255, 0)); // Body / default
                            }
                        }
                    }

                    // Create border for this detection
                    var border = new System.Windows.Controls.Border
                    {
                        Width = detection.Width,
                        Height = detection.Height,
                        BorderBrush = new SolidColorBrush(boxColor),
                        BorderThickness = new Thickness(_borderThickness),
                        CornerRadius = new CornerRadius(_cornerRadius),
                        Opacity = _boxOpacity
                    };

                    // Position the border
                    Canvas.SetLeft(border, detection.X);
                    Canvas.SetTop(border, detection.Y);

                    DetectionCanvas.Children.Add(border);

                    // Only add label if showConfidence is true
                    if (showConfidence)
                    {
                        var label = new System.Windows.Controls.TextBlock
                        {
                            Text = $"{detection.ClassName}: {Math.Round(detection.Confidence * 100, 1)}%",
                            Foreground = new SolidColorBrush(boxColor),
                            FontSize = _fontSize,
                            FontWeight = FontWeights.Bold,
                            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
                        };

                        Canvas.SetLeft(label, detection.X);
                        Canvas.SetTop(label, detection.Y - 20);

                        DetectionCanvas.Children.Add(label);
                    }
                }
            });
        }

        public void ClearAllDetections()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DetectionCanvas.Children.Clear();
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        // Clean up event subscription
        protected override void OnClosed(EventArgs e)
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            base.OnClosed(e);
        }
    }
}