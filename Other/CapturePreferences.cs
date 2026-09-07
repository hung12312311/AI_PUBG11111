using System.IO;
using System.Text.Json;
using State = Aimmy2.Class.Dictionary;

namespace Other;

internal static class CapturePreferences
{
    private static readonly object Sync = new();
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "bin", "capture.cfg");
    private static readonly string[] Keys = { "Screen Capture Method", "Scope Capture Method" };

    public static void Load()
    {
        lock (Sync)
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                using var json = JsonDocument.Parse(File.ReadAllText(FilePath));
                foreach (var key in Keys)
                    if (json.RootElement.TryGetProperty(key, out var value)
                        && value.GetString() is string method && method is "GDI+" or "DirectX" or "WGC")
                        State.dropdownState[key] = method;
            }
            catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Warning, $"Cannot load capture preferences: {ex.Message}"); }
        }
    }

    public static void Save()
    {
        MouseSensitivityProfiles.NotifyChanged();
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var values = Keys.ToDictionary(key => key, key => (string)State.dropdownState[key]);
                File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(FilePath + ".tmp", FilePath, true);
            }
            catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Warning, $"Cannot save capture preferences: {ex.Message}"); }
        }
    }
}
