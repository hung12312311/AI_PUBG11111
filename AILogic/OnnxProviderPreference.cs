namespace Aimmy2.AILogic;

internal static class OnnxProviderPreference
{
    internal static string Resolve(IReadOnlyDictionary<string, object> dropdowns,
        IReadOnlyDictionary<string, object> toggles, bool defaultDirectML = true)
    {
        if (dropdowns.TryGetValue("ONNX Provider", out var selected))
        {
            string? name = selected?.ToString()?.Trim();
            foreach (string provider in new[] { "Auto", "DirectML", "CUDA", "CPU" })
                if (string.Equals(name, provider, StringComparison.OrdinalIgnoreCase)) return provider;
        }

        // Older custom configs have no DirectML toggle: preserve their DML -> CPU policy.
        bool directML = defaultDirectML;
        if (toggles.TryGetValue("DirectML", out var legacy) && bool.TryParse(legacy?.ToString(), out bool enabled))
            directML = enabled;
        return directML ? "DirectML" : "CPU";
    }
}
