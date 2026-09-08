using System.Windows;
using System.Windows.Input;

namespace Aimmy2
{
    public partial class ExitConfirmationWindow : Window
    {
        public enum ExitChoice
        {
            None,
            Hide,
            Exit
        }

        public ExitChoice Choice { get; private set; } = ExitChoice.None;

        public ExitConfirmationWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            Choice = ExitChoice.Hide;
            Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Choice = ExitChoice.Exit;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Choice = ExitChoice.None;
            Close();
        }
    }
}
