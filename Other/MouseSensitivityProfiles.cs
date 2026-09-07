using System.IO;
using System.Text.Json;
using System.Windows;
using Aimmy2.UILibrary;
using State = Aimmy2.Class.Dictionary;

namespace Other;

internal static class MouseSensitivityProfiles
{
    internal readonly record struct Context(string Capture, int ImageSize, int Slot);
    private static readonly object Sync = new();
    private static Dictionary<string, Dictionary<int, Dictionary<int, double>>> Profiles = new();
    private static readonly double[] Defaults = { 0.8, 0.8 };
    private static bool _loaded, _dirty;
    private static int _refreshPending;
    private static event Action? Changed;
    private static readonly System.Threading.Timer SaveTimer = new(_ => Save(), null, -1, -1);
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "bin", "mouse-sensitivity.cfg");
    private static string LegacyKey(int slot) => slot == 1 ? "Slot 1 Mouse Sensitivity" : "Mouse Sensitivity (+/-)";
    private static double Valid(double value) => double.IsFinite(value) ? Math.Clamp(value, 0.01, 1) : 0.8;

    internal static Context Current(int slot)
    {
        string capture = (string)State.dropdownState["Screen Capture Method"];
        if (capture is not ("GDI+" or "DirectX" or "WGC")) capture = "GDI+";
        int size = 640;
        if (State.dropdownState.TryGetValue($"Slot {slot} Image Size", out var configured)
            && int.TryParse(Convert.ToString((object)configured), out int parsed) && parsed > 0) size = parsed;
        int actualSize = FileManager.AIManager?.GetSlotImageSize(slot) ?? 0;
        if (actualSize > 0) size = actualSize;
        return new Context(capture, size, slot);
    }

    internal static void Load()
    {
        lock (Sync)
        {
            if (_loaded) return;
            for (int slot = 1; slot <= 2; slot++) Defaults[slot - 1] = Valid(Convert.ToDouble(State.sliderSettings[LegacyKey(slot)]));
            try
            {
                if (File.Exists(FilePath)) Profiles = JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, Dictionary<int, double>>>>(File.ReadAllText(FilePath)) ?? new();
            }
            catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Warning, $"Cannot load mouse sensitivity profiles: {ex.Message}"); }
            _loaded = true;
            foreach (var sizes in Profiles.Values)
            {
                if (sizes == null) continue;
                foreach (var slots in sizes.Values)
                {
                    if (slots == null) continue;
                    foreach (int slot in slots.Keys.ToArray())
                    {
                        double value = Valid(slots[slot]);
                        if (slots[slot] != value) { slots[slot] = value; _dirty = true; }
                    }
                }
            }
            if (_dirty) SaveTimer.Change(300, System.Threading.Timeout.Infinite);
        }
        NotifyChanged();
    }

    private static double Get(Context context)
    {
        lock (Sync)
        {
            if (Profiles.TryGetValue(context.Capture, out var sizes) && sizes != null
                && sizes.TryGetValue(context.ImageSize, out var slots) && slots != null
                && slots.TryGetValue(context.Slot, out double value)) return Valid(value);
            if (_loaded) Set(context, Defaults[context.Slot - 1]);
            return Defaults[context.Slot - 1];
        }
    }

    internal static double Get(int slot) => Get(Current(slot));

    private static void Set(Context context, double value)
    {
        lock (Sync)
        {
            if (!_loaded) return;
            if (!Profiles.TryGetValue(context.Capture, out var sizes) || sizes == null) Profiles[context.Capture] = sizes = new();
            if (!sizes.TryGetValue(context.ImageSize, out var slots) || slots == null) sizes[context.ImageSize] = slots = new();
            slots[context.Slot] = Valid(value);
            _dirty = true;
            SaveTimer.Change(300, System.Threading.Timeout.Infinite);
        }
    }

    internal static void Save()
    {
        lock (Sync)
        {
            if (!_loaded || !_dirty) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(Profiles, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(FilePath + ".tmp", FilePath, true);
                _dirty = false;
            }
            catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Warning, $"Cannot save mouse sensitivity profiles: {ex.Message}"); }
        }
    }

    internal static void NotifyChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || System.Threading.Interlocked.Exchange(ref _refreshPending, 1) != 0) return;
        dispatcher.BeginInvoke(new Action(() =>
        {
            System.Threading.Interlocked.Exchange(ref _refreshPending, 0);
            Changed?.Invoke();
        }));
    }

    internal static void Bind(ASlider control, int slot)
    {
        Context displayed = Current(slot);
        bool refreshing = false;
        void Refresh()
        {
            if (!_loaded) return;
            refreshing = true;
            try
            {
                displayed = Current(slot);
                double value = Get(displayed);
                control.Slider.Value = value;
                State.sliderSettings[LegacyKey(slot)] = value;
                control.SliderTitle.Content = $"Slot {slot} Sens · {displayed.Capture} / {displayed.ImageSize}";
                control.ToolTip = $"Slot {slot}: {displayed.Capture} → {displayed.ImageSize}. Độ nhạy được lưu riêng cho tổ hợp này.";
            }
            finally { refreshing = false; }
        }
        control.Loaded += (_, _) => { Changed -= Refresh; Changed += Refresh; Refresh(); };
        control.Unloaded += (_, _) => Changed -= Refresh;
        control.Slider.ValueChanged += (_, _) =>
        {
            if (refreshing) return;
            State.sliderSettings[LegacyKey(slot)] = control.Slider.Value;
            // Save to the context actually displayed, even if a model finishes loading concurrently.
            Set(displayed, control.Slider.Value);
        };
        if (control.IsLoaded) { Changed += Refresh; Refresh(); }
    }
}
