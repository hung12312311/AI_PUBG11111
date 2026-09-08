using System.Reflection;
using System.IO;
using System.Text.Json;
using State = Aimmy2.Class.Dictionary;
using Recoil = InputLogic.RecoilManager;

internal static class ContinuousRecoilChecks
{
    public static int Run(string configPath)
    {
        var forceMethod = typeof(Recoil).GetMethod("GetContinuousForce", BindingFlags.Static | BindingFlags.NonPublic)!;
        var syncMethod = typeof(Recoil).GetMethod("SynchronizeContext", BindingFlags.Static | BindingFlags.NonPublic)!;
        float Force(int scope, double seconds) => (float)forceMethod.Invoke(null, new object[] { scope, seconds })!;
        bool Sync(int scope, int slot, bool tap = false, bool enabled = true, bool wheel = true) =>
            (bool)syncMethod.Invoke(null, new object[] { scope, slot, tap, enabled, wheel })!;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

        // Read the user's saved numbers without changing the file or starting any input hook.
        using var config = JsonDocument.Parse(File.ReadAllText(configPath));
        foreach (var entry in config.RootElement.EnumerateObject())
            if (entry.Name.StartsWith("Recoil Scope ") && entry.Value.ValueKind == JsonValueKind.Number)
                State.sliderSettings[entry.Name] = entry.Value.GetDouble();
        State.toggleState["Mouse Wheel Adjust"] = false;
        Recoil.TemporaryStrengthOffset = -1000;
        for (int scope = 1; scope <= 6; scope++)
        {
            double elapsed = 0;
            for (int stage = 1; stage <= 4; stage++)
            {
                var expected = config.RootElement.GetProperty($"Recoil Scope {scope} S{stage} Force").GetDouble();
                Check(Math.Abs(Force(scope, elapsed + .0001) - expected) < .0001, $"Scope {scope}, stage {stage} force mismatch");
                if (stage < 4) elapsed += config.RootElement.GetProperty($"Recoil Scope {scope} S{stage} Time").GetDouble();
            }
        }
        Console.WriteLine("PASS saved config: all 24 continuous stages; disabled wheel cannot cancel force");
        State.toggleState["Mouse Wheel Adjust"] = true;
        Sync(0, 1);
        Recoil.TemporaryStrengthOffset = -1000;
        Check(Force(1, 0) == 0, "Enabled wheel no longer respects its offset");
        Check(!Sync(0, 1) && Recoil.TemporaryStrengthOffset == -1000, "Unchanged context discarded adjustment");
        Check(Sync(1, 1) && Recoil.TemporaryStrengthOffset == 0, "Scope switch retained adjustment");
        Recoil.TemporaryStrengthOffset = -1000;
        Check(Sync(1, 2) && Recoil.TemporaryStrengthOffset == 0, "Model switch retained adjustment");
        Recoil.TemporaryStrengthOffset = -1000;
        Check(Sync(1, 2, wheel: false) && Recoil.TemporaryStrengthOffset == 0, "Disabled wheel retained adjustment");
        Recoil.TemporaryStrengthOffset = -1000;
        Check(Sync(1, 2, enabled: false) && Recoil.TemporaryStrengthOffset == 0, "Disabled recoil retained adjustment");
        Sync(1, 2, tap: true);
        Check(Sync(1, 2), "Tap to continuous did not reset the firing clock");
        Check(Force(2, 0) > 0, "Continuous fire did not recover after tap");
        Recoil.SetStageForce(2, "Recoil Scope 2 S1 Force", 0);
        Check(Force(2, 0) == 0, "Explicit zero force ignored");
        Console.WriteLine("PASS scope/model/mode/enable transitions, live force edit; no mouse input sent");
        return 0;
    }
}
