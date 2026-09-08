namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ARectangleBottom.xaml
    /// </summary>
    public partial class ARectangleBottom : System.Windows.Controls.UserControl
    {
        public ARectangleBottom()
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
        }
    }
}