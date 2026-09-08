using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aimmy2;
using Aimmy2.Controls;
using Other;
using State = Aimmy2.Class.Dictionary;
static class VisualLanguageChecks
{
 public static int Run()
 {
  Directory.SetCurrentDirectory(AppContext.BaseDirectory);
  var app=new App(); app.InitializeComponent(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  UiLanguage.Initialize(); UiLanguage.Current.SetLanguage("en");
  foreach(var key in State.toggleState.Keys.ToArray()) State.toggleState[key]=false;
  foreach(var key in State.minimizeState.Keys.ToArray()) State.minimizeState[key]=false;
  var owner=new MainWindow();
  var aim=new AimMenuControl();aim.Initialize(owner);
  foreach(var d in aim.AimConfigSlot1Panel.Children.OfType<UILibrary.ADropdown>())
  {
      var box=(ComboBox)d.FindName("DropdownBox");
      var wanted=d.SettingKey switch { "Slot 1 Mouse Movement Method"=>"SendInput", "Slot 1 Movement Path"=>"Linear", "Slot 1 Detection Area Type"=>"Closest to Mouse", "Slot 1 Aiming Boundaries Alignment"=>"Center", _=>null };
      if(wanted!=null)box.SelectedItem=box.Items.OfType<ComboBoxItem>().Single(x=>x.Content?.ToString()==wanted);
  }
  var settings=new SettingsMenuControl();settings.Initialize(owner);
  var models=new ModelMenuControl();models.Initialize(owner);
  var about=new AboutMenuControl();
  var window=new Window { Width=1000,Height=850,Background=new SolidColorBrush(Color.FromRgb(0,35,30)),Content=settings };
  window.Show(); Pump();
  var selector=Walk(settings).OfType<UILibrary.ADropdown>().Single(d=>d.Name=="LanguageSelector");
  ((ComboBox)selector.FindName("DropdownBox")).SelectedIndex=1;Pump();
  if(!Walk(settings).OfType<TextBlock>().Any(t=>t.Text=="Cài đặt chung")) throw new Exception("Actual settings window did not translate on selection");
  var rebuiltSlider=new Aimmy2.UILibrary.ASlider("Slot 1 Mouse Jitter","Jitter",0.1);
  if(((Label)rebuiltSlider.FindName("SliderTitle")).Content?.ToString()!="Độ rung chuột" || ((Label)rebuiltSlider.FindName("UnitLabel")).Content?.ToString()!="Độ rung") throw new Exception("New controls reverted to English");
  var rebuiltDropdown=new UILibrary.ADropdown("Slot 1 Movement Path","Slot 1 Movement Path");
  var rebuiltOptions=(ComboBox)rebuiltDropdown.FindName("DropdownBox");
  var lateOption=new ComboBoxItem { Content="Linear" };rebuiltOptions.Items.Add(lateOption);
  if(lateOption.ContentTemplate==null || lateOption.Content?.ToString()!="Linear")throw new Exception("Late dropdown item missing display translation or changed ID");
  if(UiLanguage.Text("Scope 1 (Red Dot/1x)")!="Model scope 1 (chấm đỏ/1x)")throw new Exception("Scope terminology incorrect");
  foreach(var pair in new (string,UIElement)[]{("settings",settings),("aim",aim),("models",models),("about",about)})
  {
   window.Content=pair.Item2;Pump();
   if(pair.Item1=="aim" && !Walk(aim).OfType<TextBlock>().Any(t=>t.Text=="Hỗ trợ ngắm")) throw new Exception("Lazy aim page untranslated");
   Save(window,"vi-"+pair.Item1+".png");
   if(pair.Item1=="aim")
   {
       var panel=aim.AimConfigSlot1Panel;
       var b=new RenderTargetBitmap((int)panel.ActualWidth,1000,96,96,PixelFormats.Pbgra32);b.Render(panel);
       var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));using var f=File.Create("vi-model1-panel.png");encoder.Save(f);
   }
   File.WriteAllLines("vi-"+pair.Item1+"-visible.txt",Walk(pair.Item2).OfType<TextBlock>().Select(t=>t.Text).Distinct());
   foreach(var combo in Walk(pair.Item2).OfType<ComboBox>().ToArray())
   {
    if(!combo.IsVisible || combo.Items.Count==0)continue;
    var original=combo.SelectedItem;
    combo.IsDropDownOpen=true;Pump();combo.IsDropDownOpen=false;
    if(!Equals(original,combo.SelectedItem))throw new Exception("Translation changed selection");
   }
  }
  window.Content=settings;Pump();
  var languageBox=(ComboBox)selector.FindName("DropdownBox");
  languageBox.IsDropDownOpen=true;Pump();
  foreach(var item in languageBox.Items.OfType<ComboBoxItem>())
      if(item.Background is not SolidColorBrush color || color.Color.R > 100)throw new Exception("Language item background is not dark");
  var popup=(System.Windows.Controls.Primitives.Popup)languageBox.Template.FindName("PART_Popup",languageBox);
  if(popup?.Child is FrameworkElement popupChild)
  {
      popupChild.UpdateLayout();var image=new RenderTargetBitmap((int)popupChild.ActualWidth,(int)popupChild.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(popupChild);
      var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create("language-popup.png");encoder.Save(file);
  }
  languageBox.IsDropDownOpen=false;

  ((ComboBox)selector.FindName("DropdownBox")).SelectedIndex=0;Pump();
  var guidance=new ToolTip { Content="Điều chỉnh chiều cao cửa sổ ứng dụng." };UiLanguage.Localize(guidance);
  if(guidance.Content?.ToString()!="Adjust the application window height.")throw new Exception("English tooltip failed");
  var punctuationTip = new ToolTip { Content="Độ trong suốt của khung phát hiện. 0 = trong suốt hoàn toàn, 1 = đặc." }; UiLanguage.Localize(punctuationTip);
  if(punctuationTip.Content?.ToString()!="Detection box opacity: zero is transparent, one is opaque.")throw new Exception("Guidance with commas failed to translate");
  if(UiLanguage.Text("Chưa tải model")!="No model loaded")throw new Exception("English notice failed");
  if(!Walk(settings).OfType<TextBlock>().Any(t=>t.Text=="Settings Menu"))throw new Exception("Live English restore failed");
  ((ComboBox)selector.FindName("DropdownBox")).SelectedIndex=1;Pump();
  var changing=new TextBlock { Text="Show AI Confidence" };
  window.Content=changing;Pump();UiLanguage.RefreshTree(window);
  if(changing.Text!="Hiện độ tin cậy AI")throw new Exception("Dynamic initial translation failed");
  changing.Text="Show Tracers";
  if(changing.Text!="Hiện đường nối mục tiêu")throw new Exception("Dynamic source update escaped translation");
  UiLanguage.Current.SetLanguage("en");Pump();
  if(changing.Text!="Show Tracers")throw new Exception("Dynamic original text lost");
  UiLanguage.Current.SetLanguage("vi");Pump();
  window.Dispatcher.BeginInvoke(new Action(()=>{
    var dialog=app.Windows.OfType<Window>().Single(w=>w.Title=="Lỗi");
    Save(dialog,"vi-dialog.png");
    var yes=Walk(dialog).OfType<Button>().Single(b=>b.Content?.ToString()=="Có");
    yes.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  }),DispatcherPriority.ApplicationIdle);
  if(LocalizedMessageBox.Show("A config already exists with the same name, would you like to overwrite it?","Error",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)throw new Exception("Localized dialog changed result");
  var scope=new Visuality.DetectedScopeWindow();scope.Show(true);scope.UpdateActiveSlot(2);Pump();
  if(!Walk(scope).OfType<TextBlock>().Any(t=>t.Text=="Model scope 1: ") || !Walk(scope).OfType<TextBlock>().Any(t=>t.Text=="Model scope 2"))throw new Exception("Scope overlay labels incorrect");
  Save(scope,"scope-model-labels.png");scope.Close();
  window.Close();app.Shutdown();
  Console.WriteLine("PASS actual WPF windows: live VI/EN, lazy pages, dropdown state preserved");return 0;
 }
 static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
 static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Walk(VisualTreeHelper.GetChild(root,i)))yield return child;}
 static void Save(Window w,string path){w.UpdateLayout();var b=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(w);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(path);e.Save(f);}
}
