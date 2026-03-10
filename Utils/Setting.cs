using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2ShowPlayerHandCards.Utils
{
    /// <summary>
    ///     Generic settings manager that supports multiple instances with custom paths and data models.
    ///     Provides file operations using Godot's FileAccess and System.Text.Json for serialization.
    ///     Data access is read-only, modifications must be done through provided methods.
    /// </summary>
    /// <typeparam name="T">The data model type for settings</typeparam>
    public class Setting<T> where T : class, new()
    {
        private readonly T _defaultValues;
        private readonly string _instanceName;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        ///     Initializes a new instance of the Setting class.
        /// </summary>
        /// <param name="filePath">The file path where settings will be stored (e.g., "user://settings.json")</param>
        /// <param name="defaultValues">Default values to use if file doesn't exist or fails to load</param>
        /// <param name="instanceName">Optional instance name for logging purposes</param>
        public Setting(string filePath, T? defaultValues = null, string? instanceName = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            FilePath = filePath;
            _defaultValues = defaultValues ?? new T();
            Data = new();
            _instanceName = instanceName ?? $"Setting<{typeof(T).Name}>";

            _jsonOptions = new()
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                IncludeFields = false,
            };

            Main.Logger.Debug($"[{_instanceName}] Initialized settings instance at path: {FilePath}");
        }

        /// <summary>
        ///     Gets the current settings data (read-only access).
        ///     To modify data, use Modify(), Set(), or ResetToDefaults() methods.
        /// </summary>
        public T Data { get; }

        /// <summary>
        ///     Gets the file path for this settings instance.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        ///     Event triggered when settings data changes through Load, Modify, Set, or ResetToDefaults.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        ///     Loads settings from file. If file doesn't exist or fails to load, uses default values.
        ///     Triggers Changed event after loading.
        /// </summary>
        /// <returns>True if loaded successfully, false if defaults were used</returns>
        public bool Load()
        {
            var result = FileOperations.ReadJson<T>(FilePath, _jsonOptions, _instanceName);

            if (!result.Success || result.Data == null)
            {
                Main.Logger.Info($"[{_instanceName}] Using default values due to: {result.ErrorMessage}");
                CopyProperties(_defaultValues, Data);
                BroadcastChange();
                return false;
            }

            CopyProperties(result.Data, Data);
            Main.Logger.Info($"[{_instanceName}] Successfully loaded settings from '{FilePath}'");
            BroadcastChange();
            return true;
        }

        /// <summary>
        ///     Saves current settings to file.
        /// </summary>
        /// <returns>True if saved successfully, false otherwise</returns>
        public bool Save()
        {
            var result = FileOperations.WriteJson(FilePath, Data, _jsonOptions, _instanceName);

            if (result.Success) Main.Logger.Info($"[{_instanceName}] Successfully saved settings to '{FilePath}'");

            return result.Success;
        }

        /// <summary>
        ///     Resets settings to default values and triggers Changed event.
        /// </summary>
        public void ResetToDefaults()
        {
            CopyProperties(_defaultValues, Data);
            Main.Logger.Info($"[{_instanceName}] Reset settings to default values");
            BroadcastChange();
        }

        /// <summary>
        ///     Modifies settings data using an action and triggers Changed event.
        /// </summary>
        /// <param name="modifier">Action to modify the data</param>
        /// <example>
        ///     settings.Modify(data => {
        ///     data.Volume = 80;
        ///     data.FullScreen = true;
        ///     });
        /// </example>
        public void Modify(Action<T> modifier)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (modifier == null)
            {
                Main.Logger.Warn($"[{_instanceName}] Cannot modify with null action");
                return;
            }

            try
            {
                modifier(Data);
                Main.Logger.Debug($"[{_instanceName}] Data modified");
                BroadcastChange();
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"[{_instanceName}] Error during data modification: {ex.Message}");
            }
        }

        /// <summary>
        ///     Gets a value from settings using a selector function.
        /// </summary>
        /// <typeparam name="TValue">The type of value to retrieve</typeparam>
        /// <param name="selector">Function to select the value</param>
        /// <returns>The selected value</returns>
        /// <example>
        ///     var volume = settings.Get(data => data.Volume);
        /// </example>
        public TValue Get<TValue>(Func<T, TValue> selector)
        {
            return selector == null ? throw new ArgumentNullException(nameof(selector)) : selector(Data);
        }

        /// <summary>
        ///     Sets a value in settings using a setter action and triggers Changed event.
        /// </summary>
        /// <typeparam name="TValue">The type of value to set</typeparam>
        /// <param name="setter">Action to set the value</param>
        /// <param name="value">The value to set</param>
        /// <example>
        ///     settings.Set((data, val) => data.Volume = val, 80);
        /// </example>
        public void Set<TValue>(Action<T, TValue> setter, TValue value)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (setter == null)
            {
                Main.Logger.Warn($"[{_instanceName}] Cannot set with null action");
                return;
            }

            try
            {
                setter(Data, value);
                Main.Logger.Debug($"[{_instanceName}] Value set");
                BroadcastChange();
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"[{_instanceName}] Error during value setting: {ex.Message}");
            }
        }

        /// <summary>
        ///     Deletes the settings file.
        /// </summary>
        /// <returns>True if deleted successfully or file doesn't exist, false on error</returns>
        public bool DeleteFile()
        {
            return FileOperations.DeleteFile(FilePath, _instanceName).Success;
        }

        /// <summary>
        ///     Broadcasts the Changed event to all subscribers.
        /// </summary>
        private void BroadcastChange()
        {
            Changed?.Invoke();
        }

        /// <summary>
        ///     Copies all properties from source to target using cached reflection information.
        ///     Preserves the target instance reference for performance.
        /// </summary>
        private static void CopyProperties(T source, T target)
        {
            var properties = PropertyCache.GetProperties(typeof(T));
            var propertiesSpan = properties.AsSpan();

            foreach (var prop in propertiesSpan)
                try
                {
                    var value = prop.GetValue(source);
                    prop.SetValue(target, value);
                }
                catch
                {
                    // Silently skip properties that fail to copy
                }
        }
    }
}
