using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Aimmy2;
using Aimmy2.UILibrary;
using State = Aimmy2.Class.Dictionary;
static class SensitivityProfileChecks
{
 public static int Run()
 {
  Directory.SetCurrentDirectory(AppContext.BaseDirectory);
  var app=new App();app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  var type=typeof(MainWindow).Assembly.GetType("Other.MouseSensitivityProfiles")!;
  const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
  void Call(string method)=>type.GetMethod(method,flags)!.Invoke(null,null);
  var get=type.GetMethod("Get",flags,null,new[]{typeof(int)},null)!;
  var slider1=new ASlider("Slot 1 Mouse Sensitivity","Sens",0.01);
  var slider2=new ASlider("Mouse Sensitivity (+/-)","Sens",0.01);
  var sliders=new[]{slider1,slider2};
  foreach(var control in sliders){var s=(Slider)control.FindName("Slider");s.Minimum=0.01;s.Maximum=1;s.TickFrequency=0.01;}
  var file=Path.Combine(AppContext.BaseDirectory,"bin","mouse-sensitivity.cfg");
  Directory.CreateDirectory(Path.GetDirectoryName(file)!);
  string? previous=File.Exists(file)?File.ReadAllText(file):null;
  try
  {
   File.WriteAllText(file,"{}");Call("Load");
   for(int slot=1;slot<=2;slot++)type.GetMethod("Bind",flags)!.Invoke(null,new object[]{sliders[slot-1],slot});
   var panel=new StackPanel();panel.Children.Add(slider1);panel.Children.Add(slider2);
   var window=new Window {Content=panel,Width=550,Height=250};window.Show();Pump();
   string[] methods={"GDI+","WGC","DirectX"};int[] sizes={256,640};
   void Context(int m,int z)
   {
    State.dropdownState["Screen Capture Method"]=methods[m];
    State.dropdownState["Slot 1 Image Size"]=sizes[z].ToString();
    State.dropdownState["Slot 2 Image Size"]=sizes[z].ToString();
    Call("NotifyChanged");Pump();
   }
   double Expected(int m,int z,int slot)=>0.2+m*0.2+z*0.05+slot*0.01;
   for(int m=0;m<methods.Length;m++)for(int z=0;z<sizes.Length;z++)
   {
    Context(m,z);
    for(int slot=1;slot<=2;slot++)((Slider)sliders[slot-1].FindName("Slider")).Value=Expected(m,z,slot);
   }
   Call("Save");
   using(var json=JsonDocument.Parse(File.ReadAllText(file)))
    foreach(var method in methods)foreach(var size in sizes)
     if(json.RootElement.GetProperty(method).GetProperty(size.ToString()).EnumerateObject().Count()!=2)throw new Exception("Missing model-specific values");
   void CheckAll()
   {
    for(int m=methods.Length-1;m>=0;m--)for(int z=sizes.Length-1;z>=0;z--)
    {
     Context(m,z);
     for(int slot=1;slot<=2;slot++)
     {
      double expected=Expected(m,z,slot);
      double stored=(double)get.Invoke(null,new object[]{slot})!;
      double shown=((Slider)sliders[slot-1].FindName("Slider")).Value;
      if(Math.Abs(stored-expected)>1e-9 || Math.Abs(shown-expected)>1e-9)throw new Exception($"Mismatch {methods[m]} / {sizes[z]} / model {slot}: stored={stored}, shown={shown}, expected={expected}");
     }
    }
   }
   CheckAll();
   type.GetField("Profiles",flags)!.SetValue(null,Activator.CreateInstance(type.GetField("Profiles",flags)!.FieldType));
   type.GetField("_loaded",flags)!.SetValue(null,false);
   Call("Load");Pump();CheckAll();
   // Different model sizes must select independently within one capture method.
   State.dropdownState["Screen Capture Method"]="WGC";
   State.dropdownState["Slot 1 Image Size"]="256";State.dropdownState["Slot 2 Image Size"]="640";
   Call("NotifyChanged");Pump();
   if(Math.Abs((double)get.Invoke(null,new object[]{1})!-Expected(1,0,1))>1e-9 || Math.Abs((double)get.Invoke(null,new object[]{2})!-Expected(1,1,2))>1e-9)throw new Exception("Independent model sizes were mixed");
   window.Close();app.Shutdown();
   Console.WriteLine("PASS 3 capture methods x 2 sizes x 2 models: edit, restore, on-disk reload, independent model sizes; no input sent");return 0;
  }
  finally {if(previous!=null)File.WriteAllText(file,previous);}
 }
 static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
}
