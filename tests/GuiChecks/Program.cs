using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aimmy2.Controls;
internal static class Program
{
 [STAThread] private static void Main()
 {
  var root = new DirectoryInfo(AppContext.BaseDirectory);
  while(root != null && !File.Exists(Path.Combine(root.FullName,"Aimmy2.csproj"))) root=root.Parent;
  if(root == null) return;
  Directory.SetCurrentDirectory(root.FullName);
  var app = new Application();
  app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml") });
  app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml") });
  var dashboard = new ModelDashboard();
  var window = new Window { Title="AIOK GUI verification", Width=620,Height=1000,MinWidth=380,MinHeight=320,Background=new SolidColorBrush(Color.FromRgb(23,28,41)),Content=dashboard };
  window.Loaded += (_,_) => window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() => {
    void Expand(DependencyObject node)
    {
      if (node is System.Windows.Controls.Expander expander) expander.IsExpanded = true;
      for (int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) Expand(VisualTreeHelper.GetChild(node,i));
    }
    Expand(dashboard); window.UpdateLayout();
    var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
    bitmap.Render(window);
    var encoder = new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream=File.Create("reports/gui-dashboard-expanded.png");encoder.Save(stream);
    app.Shutdown();
  }));
  app.Run(window);
 }
}
