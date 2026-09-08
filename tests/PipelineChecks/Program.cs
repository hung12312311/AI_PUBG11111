using Aimmy2.AILogic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing;
using ModelMetadata = Aimmy2.AILogic.ModelMetadata;

var report = new List<object>();
int failed = 0;
void Check(string name, Action action)
{
    try { action(); report.Add(new { Test = name, Status = "PASS" }); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; report.Add(new { Test = name, Status = "FAIL", Error = ex.Message }); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Equal(float a, float b) { if (Math.Abs(a - b) > .001) throw new Exception($"{a} != {b}"); }
Check("Startup legacy config without DirectML toggle", () => {
    var dropdowns = new Dictionary<string, object> { ["ONNX Provider"] = "Legacy" };
    var toggles = new Dictionary<string, object>();
    if (OnnxProviderPreference.Resolve(dropdowns, toggles) != "DirectML") throw new Exception("Legacy DML fallback lost");
    dropdowns.Clear();
    if (OnnxProviderPreference.Resolve(dropdowns, toggles) != "DirectML") throw new Exception("Missing provider default lost");
    dropdowns["ONNX Provider"] = "CPU";
    if (OnnxProviderPreference.Resolve(dropdowns, toggles) != "CPU") throw new Exception("Explicit provider ignored");
    dropdowns["ONNX Provider"] = "Legacy";
    toggles["DirectML"] = false;
    if (OnnxProviderPreference.Resolve(dropdowns, toggles) != "CPU") throw new Exception("Legacy CPU preference ignored");
    toggles["DirectML"] = "invalid";
    if (OnnxProviderPreference.Resolve(dropdowns, toggles) != "DirectML") throw new Exception("Invalid legacy value not handled");
});
Check("Letterbox and negative monitor origin", () => {
    var t = new CaptureTransform(new Rectangle(-1500, -200, 640, 640), 640, 384, true);
    var center = t.ModelToScreen(320, 192); Equal(center.X, -1180); Equal(center.Y, 120);
    var box = t.ModelToCapture(new RectangleF(128, 0, 384, 384)); Equal(box.Width, 640); Equal(box.X, 0);
});
Check("Stretch FOV640 to rectangular160x96", () => {
    var t = new CaptureTransform(new Rectangle(0, 0, 640, 640), 160, 96, false);
    var p = t.ModelToCapture(80, 48); Equal(p.X, 320); Equal(p.Y, 320);
});
Check("FP16 conversion", () => { Float16 f = (Float16).5f; Equal((float)f, .5f); });
Check("NCHW to NHWC/BGR/normalization", () => {
    var model = new ModelMetadata { Nhwc = true, Options = new ModelOptions { Bgr = true, InputScale = 2f/255, InputOffset = -1 } };
    float[]? buffer = null;
    var input = ModelPreprocessor.Prepare([1, 0, .5f], new CaptureTransform(new Rectangle(0,0,1,1), 1,1,false), model, ref buffer);
    Equal(input[0], 0); Equal(input[1], -1); Equal(input[2], 1);
});
Check("Raw class-aware NMS and BNC", () => {
    var model = new ModelMetadata { ClassCount = 2, Options = new ModelOptions { OutputLayout = "BNC", Postprocess = "Raw" } };
    Tensor<float> input = new DenseTensor<float>(new float[] { 50,50,20,20,.9f,.1f, 50,50,20,20,.8f,.1f, 50,50,20,20,.1f,.85f }, [1,3,6]);
    var result = ModelOutputAdapter.Canonicalize(new Dictionary<string,Tensor<float>> { ["out"] = input }, model, new CaptureTransform(new Rectangle(0,0,100,100),100,100,false), .2f);
    Equal(result.Dimensions[1],2);
});
Check("End-to-end keeps overlapping boxes and maps coordinates", () => {
    var model = new ModelMetadata { Options = new ModelOptions { Postprocess = "NmsFree" } };
    Tensor<float> input = new DenseTensor<float>(new float[] {10,10,30,30,.9f,0,10,10,30,30,.8f,0}, [2,6]);
    var result = ModelOutputAdapter.Canonicalize(new Dictionary<string,Tensor<float>> { ["out"] = input }, model, new CaptureTransform(new Rectangle(0,0,640,640),160,160,false), .2f);
    Equal(result.Dimensions[1],2); Equal(result[0,0,0],40);
});
Check("Ambiguous two-class output rejects guessing", () => {
    var model = new ModelMetadata { ClassCount = 2 };
    try { ModelOutputAdapter.Canonicalize(new Dictionary<string,Tensor<float>> { ["out"] = new DenseTensor<float>(new float[18], [1,3,6]) }, model, new CaptureTransform(new Rectangle(0,0,100,100),100,100,false), .2f); }
    catch (NotSupportedException) { return; }
    throw new Exception("Ambiguous output was accepted");
});
Check("Multiple tensors and detection count", () => {
    var outputs = new Dictionary<string,Tensor<float>> {
        ["boxes"] = new DenseTensor<float>(new float[] {10,10,20,20,30,30,40,40}, [1,2,4]),
        ["scores"] = new DenseTensor<float>(new float[] {.8f,.7f}, [1,2]),
        ["classes"] = new DenseTensor<float>(new float[] {0,1}, [1,2]),
        ["num_detections"] = new DenseTensor<float>(new float[] {1}, [1]) };
    var result = ModelOutputAdapter.Canonicalize(outputs,new ModelMetadata(),new CaptureTransform(new Rectangle(0,0,100,100),100,100,false),.2f);
    Equal(result.Dimensions[1],1);
});
if (args.Length > 0)
{
    string root = Path.GetFullPath(args[0]);
    string provider = args.Length > 1 ? args[1] : "CPU";
    foreach (string path in Directory.EnumerateFiles(Path.Combine(root,"bin","Build","bin","models","Slot1"),"*.onnx"))
    {
        string name = Path.GetFileName(path);
        Check(provider + ": " + name, () => {
            var watch = Stopwatch.StartNew();
            using var session = OnnxModelSessionFactory.Load(path,provider);
            var m = OnnxModelSessionFactory.Metadata(session);
            var size = m.ResolveSize(256);
            var t = new CaptureTransform(new Rectangle(0,0,640,640),size.Width,size.Height,m.Options.Letterbox);
            var input = new float[3*640*640];
            Array.Fill(input,.5f);
            using var run = new RunOptions();
            var output = OnnxModelSessionFactory.Run(session,input,t,run,.25f);
            report.Add(new { Model=name, Backend=m.Backend, Input=m.Input, Outputs=m.Outputs, Classes=m.ClassCount, Postprocess=m.Options.Postprocess, Resolved=size.ToString(), Detections=output.Dimensions[1], LoadAndRunMs=watch.Elapsed.TotalMilliseconds });
            if (m.Dynamic)
            foreach (var dimensions in new[] { (64,64), (192,192), (224,224), (704,704), (640,384) })
            {
                m.Options.DynamicWidth = dimensions.Item1; m.Options.DynamicHeight = dimensions.Item2;
                var resolved = m.ResolveSize(256);
                var transformed = new CaptureTransform(new Rectangle(0,0,640,640),resolved.Width,resolved.Height,false);
                try
                {
                    var dynamicOutput = OnnxModelSessionFactory.Run(session,input,transformed,run,.25f);
                    report.Add(new { Test=provider+": "+name+" "+resolved, Status="PASS", Detections=dynamicOutput.Dimensions[1] });
                }
                catch (OnnxRuntimeException ex)
                {
                    report.Add(new { Test=provider+": "+name+" "+resolved, Status="MODEL_RUNTIME_REJECTED", Error=ex.Message });
                }
            }
        });
    }
}
Directory.CreateDirectory("reports");
File.WriteAllText("reports/pipeline-checks-" + (args.Length>1?args[1]:"unit") + ".json",JsonConvert.SerializeObject(report,Formatting.Indented));
return failed == 0 ? 0 : 1;

namespace Other { internal static class LogManager { internal enum LogLevel { Warning } internal static void Log(LogLevel level,string text) => Console.WriteLine(text); } }
