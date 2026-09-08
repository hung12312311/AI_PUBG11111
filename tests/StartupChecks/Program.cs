using Aimmy2.AILogic;
using System.IO;
using System.Windows;
using State = Aimmy2.Class.Dictionary;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        string root = Path.GetFullPath(args[0]);
        int result = 1;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            AIManager? manager = null;
            try
            {
                foreach (string key in State.toggleState.Keys.ToArray()) State.toggleState[key] = false;
                State.toggleState.Remove("DirectML");
                State.dropdownState["ONNX Provider"] = "Legacy";
                State.dropdownState["Screen Capture Method"] = "GDI+";
                string first = Path.Combine(root,"bin","Build","bin","models","Slot1","best_256x256_fp32_YOLO26.onnx");
                string second = Path.Combine(root,"bin","Build","bin","models","Slot2","best_256x256_fp32_YOLO26.onnx");
                manager = new AIManager(first);
                await manager.Initialization;
                if (manager.GetModelMetadata(1) == null) throw new Exception("Slot 1 initialization failed");
                await manager.LoadSecondaryModel(second, false);
                if (manager.GetModelMetadata(2) == null) throw new Exception("Slot 2 initialization failed");
                Console.WriteLine("PASS startup loads both saved slots with no DirectML key");
                await manager.LoadModelAsync(first);
                if (manager.GetModelMetadata(1) == null) throw new Exception("Primary reload failed");
                Console.WriteLine("PASS primary reload with no DirectML key");
                var previous = manager.GetModelMetadata(2);
                bool rejected = false;
                try { await manager.LoadSecondaryModel(Path.Combine(AppContext.BaseDirectory,"missing.onnx"), false); }
                catch (NotSupportedException) { rejected = true; }
                if (!rejected || !ReferenceEquals(previous, manager.GetModelMetadata(2))) throw new Exception("Failed load replaced the working secondary model");
                Console.WriteLine("PASS failed secondary load preserves working model");
                var empty = await manager.MeasureLiveAsync(null, CancellationToken.None);
                if (empty.Frames != 0) throw new Exception("Inactive pipeline invented inference samples");
                Console.WriteLine("PASS inactive measurement returns zero frames after five seconds");
                using (var cancel = new CancellationTokenSource(150))
                {
                    var started = System.Diagnostics.Stopwatch.StartNew();
                    try { await manager.MeasureLiveAsync(null, cancel.Token); throw new Exception("Cancellation ignored"); }
                    catch (OperationCanceledException) { }
                    if (started.Elapsed.TotalSeconds > 1) throw new Exception("Cancellation blocked");
                }
                Console.WriteLine("PASS measurement cancels promptly");
                var record = typeof(AIManager).GetMethod("RecordBenchmark", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                int samples = 0;
                var measuring = manager.MeasureLiveAsync(null, CancellationToken.None);
                while (!measuring.IsCompleted)
                {
                    record.Invoke(manager, new object[] { "ModelInference", 2.5 });
                    record.Invoke(manager, new object[] { "ScreenGrab", 1.0 });
                    samples++;
                    await Task.Delay(30);
                }
                var measured = await measuring;
                if (measured.Frames < samples - 1 || measured.Frames > samples || Math.Abs(measured.InferenceMs - 2.5) > .001)
                    throw new Exception("Measurement did not use interval counter deltas");
                Console.WriteLine("PASS live measurement counts concurrent samples without stopping their producer");
                var gate = typeof(AIManager).GetField("_modelLock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(manager)!;
                using var entered = new ManualResetEventSlim();
                var held = Task.Run(() => { lock (gate) { entered.Set(); Thread.Sleep(500); } });
                entered.Wait();
                var polling = System.Diagnostics.Stopwatch.StartNew();
                if (manager.TryGetModelMetadata(1, out _) || polling.ElapsedMilliseconds > 100) throw new Exception("UI snapshot blocks on GPU lock");
                await held;
                Console.WriteLine("PASS dashboard snapshot does not block behind model lock");
                State.dropdownState["Screen Capture Method"] = "DirectX";
                var fresh = manager.GetPerformanceSnapshot();
                if (fresh.ContainsKey("ModelInference") || fresh["InferenceFPS"] != 0) throw new Exception("Old method samples leaked");
                record.Invoke(manager, new object[] { "ScreenGrab", 4.0 });
                record.Invoke(manager, new object[] { "ModelInference", 9.0 });
                var recent = manager.GetPerformanceSnapshot();
                if(recent["ScreenGrab"] != 4 || recent["ModelInference"] != 9) throw new Exception("Capture/inference timing mixed");
                await Task.Delay(1100);
                var expired=manager.GetPerformanceSnapshot();
                if(expired.ContainsKey("ScreenGrab") || expired.ContainsKey("ModelInference") || expired["InferenceFPS"] != 0) throw new Exception("Stale performance retained");
                State.dropdownState["Screen Capture Method"] = "GDI+";
                Console.WriteLine("PASS rolling metrics separate capture/inference, clear on method switch, expire after one second");
                result = 0;
            }
            catch (Exception ex) { Console.WriteLine("FAIL " + ex); }
            finally { manager?.Dispose(); app.Shutdown(); }
        };
        app.Run();
        return result;
    }
}
