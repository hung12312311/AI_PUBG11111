using Aimmy2.Class;
using Microsoft.Win32;
using System.IO;
using UserControl = System.Windows.Controls.UserControl;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for AFileLocator.xaml
    /// </summary>
    public partial class AFileLocator : UserControl
    {
        private OpenFileDialog openFileDialog = new OpenFileDialog();
        private string main_dictionary_path { get; set; }
        private string OFDFilter = "All files (*.*)|*.*";
        private string DefaultLocationExtension = "";
        public event System.Action<string>? FileChanged;

        public AFileLocator(string title, string dictionary_path, string FileFilter = "All files (*.*)|*.*", string DLExtension = "")
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
            DropdownTitle.Content = title;
            global::Other.UiLanguage.Localize(DropdownTitle);

            main_dictionary_path = dictionary_path;
            string fullPath = Dictionary.filelocationState[main_dictionary_path];
            FileLocationTextbox.Text = GetDisplayPath(fullPath);

            OFDFilter = FileFilter;
            DefaultLocationExtension = DLExtension;
        }

        private string GetDisplayPath(string fullPath)
        {
            try
            {
                string baseDir = Directory.GetCurrentDirectory();
                if (fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                {
                    return fullPath.Substring(baseDir.Length).TrimStart('\\', '/');
                }
            }
            catch { }
            return fullPath;
        }

        private string GetRelativePath(string fullPath)
        {
            try
            {
                string baseDir = Directory.GetCurrentDirectory();
                if (fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                {
                    // Trả về đường dẫn tương đối
                    return fullPath.Substring(baseDir.Length).TrimStart('\\', '/');
                }
            }
            catch { }
            // Nếu không nằm trong thư mục base, giữ nguyên đường dẫn tuyệt đối
            return fullPath;
        }

        private string GetAbsolutePath(string path)
        {
            try
            {
                // Nếu đã là đường dẫn tuyệt đối, trả về nguyên
                if (Path.IsPathRooted(path))
                    return path;
                
                // Nếu là đường dẫn tương đối, kết hợp với base directory
                return Path.Combine(Directory.GetCurrentDirectory(), path);
            }
            catch { }
            return path;
        }

        private void OpenFileB_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.openfiledialog?view=windowsdesktop-8.0
            // Nori

            openFileDialog.InitialDirectory = Directory.GetCurrentDirectory() + DefaultLocationExtension;
            openFileDialog.Filter = OFDFilter;

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;
                
                // Hiển thị đường dẫn tương đối
                FileLocationTextbox.Text = GetDisplayPath(selectedPath);
                
                // Lưu đường dẫn tương đối để portable
                Dictionary.filelocationState[main_dictionary_path] = GetRelativePath(selectedPath);
                
                // Gửi đường dẫn tuyệt đối cho FileChanged event
                FileChanged?.Invoke(selectedPath);
            }
        }

        public void SetFilePath(string path)
        {
            FileLocationTextbox.Text = GetDisplayPath(path);
            Dictionary.filelocationState[main_dictionary_path] = GetRelativePath(path);
        }
    }
}