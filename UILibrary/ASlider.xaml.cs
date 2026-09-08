using Aimmy2.Theme;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ASlider.xaml
    /// </summary>
    public partial class ASlider : UserControl
    {
        public ASlider(string Text, string NotifierText, double ButtonSteps, string? tooltip = null)
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);

            SliderTitle.Content = Text;
            global::Other.UiLanguage.Localize(SliderTitle);
            UnitLabel.Content = NotifierText;
            global::Other.UiLanguage.Localize(UnitLabel);

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is System.Windows.Style style)
                    tt.Style = style;
                ToolTip = tt;
                global::Other.UiLanguage.Localize(tt);
            }

            Slider.ValueChanged += (s, e) =>
            {
                if (!ValueInput.IsFocused)
                    ValueInput.Text = $"{Slider.Value:F2}";
            };

            ValueInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ValidateInput();
                    Keyboard.ClearFocus();
                }
            };
            
            ValueInput.LostFocus += (s, e) => ValidateInput();

            SubtractOne.Click += (s, e) => UpdateSliderValue(-ButtonSteps);
            AddOne.Click += (s, e) => UpdateSliderValue(ButtonSteps);

            // Register buttons for theme updates when loaded
            Loaded += (s, e) =>
            {
                ThemeManager.RegisterElement(SubtractOne);
                ThemeManager.RegisterElement(AddOne);
            };
        }

        private void ValidateInput()
        {
            // Both separators mean a decimal point here, never a thousands separator.
            string input = ValueInput.Text.Trim().Replace(',', '.');
            if (double.TryParse(input, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out double val) && double.IsFinite(val))
            {
                if (val < Slider.Minimum) val = Slider.Minimum;
                if (val > Slider.Maximum) val = Slider.Maximum;
                Slider.Value = val;
            }
            ValueInput.Text = $"{Slider.Value:F2}";
        }

        private void UpdateSliderValue(double change)
        {
            Slider.Value = Math.Round(Slider.Value + change, 2);
        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
        }

        private void Slider_MouseUp_1(object sender, MouseButtonEventArgs e)
        {
            global::Other.LocalizedMessageBox.Show($"{Slider.Value:F2}");
        }
    }
}
