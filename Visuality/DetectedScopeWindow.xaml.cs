using Aimmy2.Class;
using System.Windows;

namespace Visuality
{
    public partial class DetectedScopeWindow : Window
    {
        public DetectedScopeWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
            
            // Position at top-right corner
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
            this.Top = 20;
            
            // Initially hidden
            // Initially hidden
            this.Visibility = Visibility.Collapsed;
            this.SourceInitialized += (s, e) => MakeClickThrough();
        }

        private void MakeClickThrough()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            ClickThroughOverlay.MakeClickThrough(hwnd);
        }

        public void UpdateSlot1(string scopeName)
        {
            Dispatcher.Invoke(() =>
            {
                Slot1Text.Text = scopeName;
            });
        }

        public void UpdateSlot2(string scopeName)
        {
            Dispatcher.Invoke(() =>
            {
                Slot2Text.Text = scopeName;
            });
        }

        public void UpdateActiveSlot(int slotNumber)
        {
            Dispatcher.Invoke(() =>
            {
                ActiveSlotText.Text = $"Model scope {slotNumber}";
            });
        }

        public void UpdateRecoilAdj(string value)
        {
            Dispatcher.Invoke(() =>
            {
                RecoilAdjText.Text = value;
            });
        }

        public void ForceReposition()
        {
            Dispatcher.Invoke(() =>
            {
                this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
                this.Top = 20;
            });
        }

        public void Show(bool show)
        {
            Dispatcher.Invoke(() =>
            {
                this.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            });
        }
    }
}
