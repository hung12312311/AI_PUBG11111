using Aimmy2.Class;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for ADropdown.xaml
    /// </summary>
    public partial class ADropdown : UserControl
    {
        public string SettingKey => main_dictionary_path;
        private string main_dictionary_path { get; set; }

        public ADropdown(string title, string dictionary_path, string? tooltip = null)
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
            DropdownTitle.Content = title;
            global::Other.UiLanguage.Localize(DropdownTitle);
            main_dictionary_path = dictionary_path;
            global::Other.UiLanguage.PrepareDropdown(DropdownBox);

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is System.Windows.Style style)
                    tt.Style = style;
                ToolTip = tt;
                global::Other.UiLanguage.Localize(tt);
            }
        }

        private void DropdownBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItemContent = ((ComboBoxItem)DropdownBox.SelectedItem)?.Content?.ToString();
            if (selectedItemContent != null)
            {
                Dictionary.dropdownState[main_dictionary_path] = selectedItemContent;
            }
        }
    }
}