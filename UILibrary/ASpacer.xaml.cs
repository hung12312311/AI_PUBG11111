namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ASpacer.xaml
    /// </summary>
    public partial class ASpacer : System.Windows.Controls.UserControl
    {
        public ASpacer()
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
        }
    }
}