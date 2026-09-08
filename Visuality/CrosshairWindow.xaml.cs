using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Aimmy2.Class;
using Aimmy2.Theme;
using Class;

namespace Visuality
{
    public partial class CrosshairWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private bool _isInitialized = false;
        private DispatcherTimer _mouseCheckTimer;

        public CrosshairWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);

            // Subscribe to display changes
            DisplayManager.DisplayChanged += OnDisplayChanged;

            // Subscribe to theme/background exclusion
            ThemeManager.ExcludeWindowFromBackground(this);

            // Subscribe to property changes
            PropertyChanger.ReceiveCrosshairColor = UpdateCrosshairColor;
            PropertyChanger.ReceiveCrosshairSize = UpdateCrosshairSize;

            // Load initial settings
            LoadInitialSettings();

            // Initialize timer (check mouse buttons every 15ms)
            _mouseCheckTimer = new DispatcherTimer(DispatcherPriority.Render);
            _mouseCheckTimer.Interval = TimeSpan.FromMilliseconds(15);
            _mouseCheckTimer.Tick += MouseCheckTimer_Tick;
            _mouseCheckTimer.Start();
        }

        private void LoadInitialSettings()
        {
            if (Dictionary.colorState.TryGetValue("Crosshair Color", out var colorHex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorHex.ToString());
                    UpdateCrosshairColor(color);
                }
                catch { }
            }
            if (Dictionary.sliderSettings.TryGetValue("Crosshair Size", out var sizeValue))
            {
                try
                {
                    UpdateCrosshairSize(Convert.ToDouble(sizeValue));
                }
                catch { }
            }
        }

        private void UpdateCrosshairColor(Color newColor)
        {
            Dot.Fill = new SolidColorBrush(newColor);
        }

        private void UpdateCrosshairSize(double newSize)
        {
            Dot.Width = newSize;
            Dot.Height = newSize;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Make window click-through
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);

            if (!_isInitialized)
            {
                _isInitialized = true;
                ForceReposition();
            }
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ForceReposition();
            });
        }

        public void ForceReposition()
        {
            try
            {
                var hwnd = _isInitialized ? new WindowInteropHelper(this).Handle : IntPtr.Zero;

                this.WindowState = WindowState.Normal;

                this.Left = DisplayManager.ScreenLeft / WinAPICaller.scalingFactorX;
                this.Top = DisplayManager.ScreenTop / WinAPICaller.scalingFactorY;
                this.Width = DisplayManager.ScreenWidth / WinAPICaller.scalingFactorX;
                this.Height = DisplayManager.ScreenHeight / WinAPICaller.scalingFactorY;

                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, IntPtr.Zero,
                        DisplayManager.ScreenLeft,
                        DisplayManager.ScreenTop,
                        DisplayManager.ScreenWidth,
                        DisplayManager.ScreenHeight,
                        SWP_NOZORDER | SWP_NOACTIVATE);
                }

                // Keep Normal: explicit monitor bounds provide full-screen coverage without activation.
                this.UpdateLayout();
            }
            catch (Exception)
            {
            }
        }

        private void MouseCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (!Dictionary.toggleState.TryGetValue("Virtual Crosshair", out var enabled) || !(bool)enabled)
            {
                if (this.Visibility != Visibility.Collapsed)
                {
                    this.Visibility = Visibility.Collapsed;
                }
                return;
            }

            // Ensure window is visible since it's enabled
            if (this.Visibility != Visibility.Visible)
            {
                this.Visibility = Visibility.Visible;
            }

            // Read configured bindings
            string bind1 = Dictionary.bindingSettings.ContainsKey("Crosshair Hide Key 1") 
                ? Dictionary.bindingSettings["Crosshair Hide Key 1"].ToString() 
                : "None";

            bool isBind1Active = (bind1 != "None" && !string.IsNullOrEmpty(bind1));

            bool isBind1Held = isBind1Active && InputLogic.InputBindingManager.IsHoldingBinding("Crosshair Hide Key 1");

            bool shouldHide = isBind1Active && isBind1Held;

            if (shouldHide)
            {
                if (Dot.Visibility != Visibility.Collapsed)
                {
                    Dot.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (Dot.Visibility != Visibility.Visible)
                {
                    Dot.Visibility = Visibility.Visible;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _mouseCheckTimer.Stop();
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            base.OnClosed(e);
        }
    }
}
