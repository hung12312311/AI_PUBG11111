using Aimmy2;
using Aimmy2.Controls;
using Aimmy2.UILibrary;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using State = Aimmy2.Class.Dictionary;
class Program
{
 [STAThread] static int Main(string[] args)
 {
  try
  {
   if(args.Contains("--continuous-recoil")) return ContinuousRecoilChecks.Run(args.Last());
   if(args.Contains("--sensitivity-profiles")) return SensitivityProfileChecks.Run();
   if(args.Contains("--visual-language")) return VisualLanguageChecks.Run();
   Directory.SetCurrentDirectory(AppContext.BaseDirectory);
   Directory.CreateDirectory("bin");
   File.WriteAllText("bin/window.cfg", "{\"WindowWidth\":920,\"WindowHeight\":760}");
   var app = new App(); app.InitializeComponent();
   var splash = new Window { Width=560, Height=560 }; app.MainWindow=splash;
   Other.UiLanguage.Initialize(); Other.UiLanguage.Current.SetLanguage("en");
   var main = new MainWindow();
   if(main.Width!=920 || main.Height!=760) throw new Exception("Saved size not restored");
   var settings = new SettingsMenuControl();
   typeof(SettingsMenuControl).GetField("_mainWindow",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(settings,main);
   typeof(SettingsMenuControl).GetMethod("LoadSettingsConfig",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(settings,null);
   var sliders = settings.SettingsConfigPanel.Children.OfType<ASlider>().ToArray();
   ((Slider)sliders.Single(x=>((Label)x.FindName("SliderTitle")).Content.ToString()=="Window Width").FindName("Slider")).Value=1030;
   ((Slider)sliders.Single(x=>((Label)x.FindName("SliderTitle")).Content.ToString()=="Window Height").FindName("Slider")).Value=820;
   if(main.Width!=1030 || main.Height!=820 || splash.Width!=560 || splash.Height!=560) throw new Exception("Release slider changed splash instead of owner");
   Console.WriteLine("PASS Release sliders update owning window while splash is Application.MainWindow");
   typeof(MainWindow).GetMethod("SaveWindowSize",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,null);
   var restored=new MainWindow();
   if(restored.Width!=1030 || restored.Height!=820) throw new Exception("Resize round trip failed");
   Console.WriteLine("PASS window size persists across reconstruction");
   var overlay=new Visuality.DetectedPlayerWindow();
   foreach(bool parent in new[]{false,true}) foreach(bool child in new[]{false,true})
   {
    State.toggleState["Show Detected Player"]=parent;State.toggleState["Show Detection Performance"]=child;
    overlay.RefreshPerformanceOverlay();
    var badge=(Border)overlay.FindName("PerformanceBadge");
    if((badge.Visibility==Visibility.Visible)!=(parent&&child)) throw new Exception("ESP performance gate failed");
   }
   Console.WriteLine("PASS ESP performance visible only when both switches enabled (four combinations)");
   var weapon = Aimmy2.AILogic.WeaponSlotManager.Instance;
   var wt=weapon.GetType();
   wt.GetField("_slot1ScopeIndex",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(weapon,5);
   wt.GetField("_slot2ScopeIndex",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(weapon,0);
   var apply=wt.GetMethod("ApplySlot",BindingFlags.NonPublic|BindingFlags.Instance)!;
   apply.Invoke(weapon,new object[]{1});
   if(InputLogic.RecoilManager.SelectedScopeIndex!=0) throw new Exception("Slot 1 Red Dot mapping wrong");
   apply.Invoke(weapon,new object[]{2});
   if(InputLogic.RecoilManager.SelectedScopeIndex!=5) throw new Exception("Slot 2 8x mapping wrong");
   Console.WriteLine("PASS distinct scopes stay associated with slot 1 and slot 2");
   var gate=wt.GetField("_slotApplyLock",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(weapon)!;
   Task refresh;
   using var started=new ManualResetEventSlim();
   lock(gate)
   {
    apply.Invoke(weapon,new object[]{1});
    refresh=Task.Run(()=>{started.Set();wt.GetMethod("ApplyCurrentSlot",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(weapon,null);});
    started.Wait();
    apply.Invoke(weapon,new object[]{2});
   }
   refresh.GetAwaiter().GetResult();
   if(InputLogic.RecoilManager.SelectedScopeIndex!=5 || (int)wt.GetField("_activeSlot",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(weapon)!=2)
    throw new Exception("Background scan restored stale slot");
   Console.WriteLine("PASS pending scan preserves the latest weapon key selection");
   foreach(var name in new[]{"CapturePerformance","InferencePerformance","FpsPerformance"})
    if(overlay.FindName(name) is not TextBlock metric || string.IsNullOrEmpty(metric.Text)) throw new Exception("Missing ESP metric");
   Console.WriteLine("PASS ESP exposes all three requested metrics");
   var aimType=typeof(Aimmy2.AILogic.AIManager);
   var detached=(Aimmy2.AILogic.AIManager)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(aimType);
   var coordinates=aimType.GetMethod("CalculateCoordinates",BindingFlags.NonPublic|BindingFlags.Instance)!;
   var refAim=new LegacyAimReference();
   State.toggleState["Aim Assist"]=true;
   int cases=0;
   foreach(int slot in new[]{1,2}) foreach(int size in new[]{160,256,320,640})
   foreach(bool percentage in new[]{false,true}) foreach(string alignment in new[]{"Top","Center","Bottom"})
   {
    Aimmy2.AILogic.AIManager.ActiveSlot=slot;
    string prefix=slot==1?"Slot 1 ":"";
    State.toggleState[prefix+"X Axis Percentage Adjustment"]=percentage;
    State.toggleState[prefix+"Y Axis Percentage Adjustment"]=percentage;
    State.sliderSettings[prefix+"X Offset (Left/Right)"]=3.0;
    State.sliderSettings[prefix+"Y Offset (Up/Down)"]=-4.0;
    State.sliderSettings[prefix+"X Offset (%)"]=35.0;
    State.sliderSettings[prefix+"Y Offset (%)"]=70.0;
    State.dropdownState["Aiming Boundaries Alignment"]=alignment;
    var prediction=new Aimmy2.AILogic.Prediction { Rectangle=new System.Drawing.RectangleF(size/2+20,size/2-30,18,40), Confidence=.9f };
    float sx=1920f/size,sy=1080f/size;
    refAim.CalculateCoordinates(null!,prediction,sx,sy);
    coordinates.Invoke(detached,new object?[]{null,prediction,sx,sy});
    int x=(int)aimType.GetProperty("detectedX",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(detached)!;
    int y=(int)aimType.GetProperty("detectedY",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(detached)!;
    if(x!=refAim.detectedX || y!=refAim.detectedY) throw new Exception($"Mouse response differs from original in slot {slot}, size {size}");
    cases++;
   }
   Console.WriteLine($"PASS {cases} aim-coordinate cases match original 409acd3 (no mouse input sent)");
   InputLogic.RecoilManager.SelectedScopeIndex=0;
   InputLogic.RecoilManager.TemporaryStrengthOffset=20;
   InputLogic.RecoilManager.SetStageForce(1,"Recoil Scope 1 S1 Force",2.0);
   var get=typeof(InputLogic.RecoilManager).GetMethod("GetSetting",BindingFlags.NonPublic|BindingFlags.Static)!;
   float force=(float)get.Invoke(null,new object[]{"Recoil Scope 1 S1 Force",0f})!;
   if(force!=2 || InputLogic.RecoilManager.TemporaryStrengthOffset!=0) throw new Exception("Live force edit kept stale value or wheel offset");
   InputLogic.RecoilManager.SetStageForce(1,"Recoil Scope 1 S1 Force",0.0);
   if((float)get.Invoke(null,new object[]{"Recoil Scope 1 S1 Force",9f})! != 0) throw new Exception("Zero force ignored");
   Console.WriteLine("PASS recoil loop reads edited force immediately, including zero, and clears active wheel offset");
   var accumulate=typeof(InputLogic.RecoilManager).GetMethod("AccumulateContinuousPull",BindingFlags.NonPublic|BindingFlags.Static)!;
   int Pull(float force,int ticks)
   {
    object[] input={0f,force}; int total=0;
    for(int i=0;i<ticks;i++) total+=(int)accumulate.Invoke(null,input)!;
    return total;
   }
   if(Pull(2f,10)!=10 || Pull(.125f,16)!=1 || Pull(0f,100)!=0 || Math.Abs(Pull(.01f,2000)-10)>1)
    throw new Exception("Continuous recoil gain or fractional accumulation failed");
   Console.WriteLine("PASS recoil strength halves, sub-one forces accumulate, zero does not move");
   var assembly=typeof(Aimmy2.AILogic.AIManager).Assembly;
   var profiles=assembly.GetType("Other.MouseSensitivityProfiles")!;
   var current=profiles.GetMethod("Current",BindingFlags.NonPublic|BindingFlags.Static)!;
   State.dropdownState["Screen Capture Method"]="GDI+";
   var gdi=current.Invoke(null,new object[]{2});
   State.dropdownState["Screen Capture Method"]="WGC";
   if(Equals(gdi,current.Invoke(null,new object[]{2}))) throw new Exception("WGC sensitivity context was merged with GDI+");
   Console.WriteLine("PASS WGC and GDI+ have independent sensitivity profile contexts");
   var dd=assembly.GetType("MouseMovementLibraries.ddxoftSupport.DdxoftMain")!;
   if((bool)dd.GetProperty("IsReady")!.GetValue(null)!) throw new Exception("Driver unexpectedly initialized in test");
   dd.GetMethod("Move")!.Invoke(null,new object[]{1,1});
   dd.GetMethod("Button")!.Invoke(null,new object[]{1});
   var pending=new TaskCompletionSource<bool>();
   var loading=dd.GetField("_loading",BindingFlags.NonPublic|BindingFlags.Static)!;
   loading.SetValue(null,pending.Task);
   var load=dd.GetMethod("Load")!;
   if(!ReferenceEquals(load.Invoke(null,null),pending.Task) || !ReferenceEquals(load.Invoke(null,null),pending.Task)) throw new Exception("Concurrent driver init was not shared");
   pending.SetResult(false);loading.SetValue(null,null);
   Console.WriteLine("PASS ddxoft ignores input before readiness and shares pending initialization (no driver loaded)");
   Other.UiLanguage.Initialize();
   var translated = new Label { Content = "Slot 2 Image Size" };
   Other.UiLanguage.Localize(translated);
   var option = new ComboBoxItem { Content = "Mouse Sensitivity" };
   Other.UiLanguage.Localize(option);
   var languageDropdown = new UILibrary.ADropdown("Screen Capture Method","Screen Capture Method");
   var languageOption = main.AddDropdownItem(languageDropdown,"GDI+");
   Other.UiLanguage.Localize((Label)languageDropdown.FindName("DropdownTitle"));
   var before = State.dropdownState["Screen Capture Method"];
   Other.UiLanguage.Current.SetLanguage("vi");
   if(translated.Content.ToString() != "Kích thước ảnh") throw new Exception("Vietnamese label failed");
   if(option.Content.ToString() != "Mouse Sensitivity" || State.dropdownState["Screen Capture Method"] != before) throw new Exception("Language changed setting IDs");
   if(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"bin","language.cfg")) != "vi") throw new Exception("Language was not saved");
   languageOption.RaiseEvent(new RoutedEventArgs(ComboBoxItem.SelectedEvent));
   if(State.dropdownState["Screen Capture Method"] != "GDI+" || State.dropdownState.ContainsKey("Phương thức chụp màn hình")) throw new Exception("Localized dropdown wrote translated key");
   ((ComboBox)settings.SettingsConfigPanel.Children.OfType<UILibrary.ADropdown>().Single(d=>d.Name=="LanguageSelector").FindName("DropdownBox")).SelectedIndex=1;
   void TranslateTree(DependencyObject node)
   {
       if(node is FrameworkElement fe) Other.UiLanguage.Localize(fe);
       foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) TranslateTree(child);
   }
   TranslateTree(settings.SettingsConfigPanel);
   settings.SettingsConfigPanel.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(24,27,34));
   settings.SettingsConfigPanel.Measure(new Size(620,1000));
   settings.SettingsConfigPanel.Arrange(new Rect(0,0,620,1000));
   settings.SettingsConfigPanel.UpdateLayout();
   var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(620,1000,96,96,System.Windows.Media.PixelFormats.Pbgra32);
   bitmap.Render(settings.SettingsConfigPanel);
   var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
   encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
   using(var screenshot=File.Create("language-settings.png")) encoder.Save(screenshot);
   Other.UiLanguage.Current.SetLanguage("en");
   if(translated.Content.ToString() != "Image Size") throw new Exception("English switch failed");
   Console.WriteLine("PASS language switches live, persists, and preserves dropdown IDs");
   for(int shot=1;shot<=5;shot++) State.sliderSettings[$"Recoil Scope 1 Tap Shot {shot}"]=shot*2.0;
   State.sliderSettings["Recoil Scope 1 Tap Shot 6"]=199.0;
   State.sliderSettings["Recoil Scope 1 Tap Shot 15"]=200.0;
   if(InputLogic.RecoilManager.TapShotCount!=5)throw new Exception("Tap stage count incorrect");
   for(int shot=1;shot<=20;shot++)if(InputLogic.RecoilManager.GetTapShotDistance(1,shot)!=Math.Min(shot,5)*2)throw new Exception("Tap stage selection exceeded shot 5");
   Console.WriteLine("PASS 5 tap stages; shots 6 onward use shot 5, ignoring legacy stages 6–15");
   return 0;
  }
  catch(Exception e){Console.WriteLine(e);return 1;}
 }
}

