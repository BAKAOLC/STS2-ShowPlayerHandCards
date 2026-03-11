using Godot;
using STS2ShowPlayerHandCards.Utils;

namespace STS2ShowPlayerHandCards.Data
{
    /// <summary>
    ///     Settings data model for the mod.
    /// </summary>
    public class ModSettingsData
    {
        /// <summary>
        ///     The key used to toggle hand card visibility.
        ///     Stored as string for JSON serialization.
        /// </summary>
        public string ToggleKey { get; set; } = InputHandler.DefaultToggleKey.ToString();
    }

    /// <summary>
    ///     Manages mod settings including keybindings.
    /// </summary>
    public static class ModSettings
    {
        private static readonly Setting<ModSettingsData> Settings;

        static ModSettings()
        {
            Settings = new(
                $"user://mod-configs/{Const.ModId}/settings.json",
                new(),
                "ModSettings"
            );
        }

        public static event Action? Changed
        {
            add => Settings.Changed += value;
            remove => Settings.Changed -= value;
        }

        public static void Load()
        {
            if (!Settings.Load()) Settings.Save();
            ApplySettings();
        }

        /// <summary>
        ///     Saves current settings to file.
        /// </summary>
        public static void Save()
        {
            Settings.Save();
        }

        /// <summary>
        ///     Gets the current toggle key.
        /// </summary>
        public static Key GetToggleKey()
        {
            if (Enum.TryParse<Key>(Settings.Data.ToggleKey, out var key))
                return key;
            return InputHandler.DefaultToggleKey;
        }

        /// <summary>
        ///     Sets the toggle key and saves settings.
        /// </summary>
        public static void SetToggleKey(Key key)
        {
            Settings.Modify(data => data.ToggleKey = key.ToString());
            InputHandler.CurrentKey = key;
            Save();
        }

        /// <summary>
        ///     Applies loaded settings to the mod.
        /// </summary>
        private static void ApplySettings()
        {
            InputHandler.CurrentKey = GetToggleKey();
        }
    }
}
