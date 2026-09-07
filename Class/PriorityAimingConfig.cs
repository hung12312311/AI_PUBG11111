using Newtonsoft.Json;
using System.IO;

namespace Class
{
    internal static class PriorityAimingConfig
    {
        private const string ConfigPath = "bin\\priority_aiming.cfg";

        private static readonly string[] ToggleKeys =
        [
            "Slot 1 Priority Aiming",
            "Slot 2 Priority Aiming"
        ];

        private static readonly string[] BindingKeys =
        [
            "Slot 1 Priority Key",
            "Slot 2 Priority Key"
        ];

        private static readonly string[] ObsoleteKeys =
        [
            "Slot 1 Target Choice",
            "Slot 2 Target Choice",
            "Slot 1 Aim Priority",
            "Slot 2 Aim Priority"
        ];

        private static readonly HashSet<string> AllKeys = ToggleKeys
            .Concat(BindingKeys)
            .Concat(ObsoleteKeys)
            .ToHashSet(StringComparer.Ordinal);

        public static bool IsPriorityKey(string key) => AllKeys.Contains(key);

        public static Dictionary<string, dynamic> WithoutPriorityKeys(Dictionary<string, dynamic> source)
        {
            return source
                .Where(kvp => !IsPriorityKey(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public static void Load()
        {
            EnsureDirectory();
            RemoveObsoleteKeys();

            if (!File.Exists(ConfigPath))
            {
                Save();
                return;
            }

            var config = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(File.ReadAllText(ConfigPath));
            if (config == null)
                return;

            Apply(config, ToggleKeys, Aimmy2.Class.Dictionary.toggleState);
            Apply(config, BindingKeys, Aimmy2.Class.Dictionary.bindingSettings);

            Save();
        }

        public static void Save()
        {
            EnsureDirectory();
            RemoveObsoleteKeys();
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(CreateSnapshot(), Formatting.Indented));
        }

        private static void Apply(Dictionary<string, dynamic> config, string[] keys, Dictionary<string, dynamic> target)
        {
            foreach (var key in keys)
            {
                if (config.TryGetValue(key, out var value))
                {
                    target[key] = value;
                }
            }
        }

        private static Dictionary<string, dynamic> CreateSnapshot()
        {
            var snapshot = new Dictionary<string, dynamic>();

            Copy(Aimmy2.Class.Dictionary.toggleState, ToggleKeys, snapshot);
            Copy(Aimmy2.Class.Dictionary.bindingSettings, BindingKeys, snapshot);

            return snapshot;
        }

        private static void RemoveObsoleteKeys()
        {
            foreach (var key in ObsoleteKeys)
            {
                Aimmy2.Class.Dictionary.dropdownState.Remove(key);
                Aimmy2.Class.Dictionary.sliderSettings.Remove(key);
            }
        }

        private static void Copy(Dictionary<string, dynamic> source, string[] keys, Dictionary<string, dynamic> target)
        {
            foreach (var key in keys)
            {
                if (source.TryGetValue(key, out var value))
                {
                    target[key] = value;
                }
            }
        }

        private static void EnsureDirectory()
        {
            string? directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
