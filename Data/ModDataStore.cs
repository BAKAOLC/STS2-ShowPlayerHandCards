using System.Text.Json;
using System.Text.Json.Serialization;
using STS2ShowPlayerHandCards.Data.Models;
using STS2ShowPlayerHandCards.Utils;
using STS2ShowPlayerHandCards.Utils.Persistence;
using STS2ShowPlayerHandCards.Utils.Persistence.Migration;

namespace STS2ShowPlayerHandCards.Data
{
    /// <summary>
    ///     Unified data store for all mod persistent data.
    ///     Uses key-based registration to avoid hardcoded per-data properties and methods.
    /// </summary>
    public class ModDataStore
    {
        public const string SettingsKey = "settings";

        private static ModDataStore? _instance;

        private readonly Dictionary<string, IRegisteredDataEntry> _entries =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly JsonSerializerOptions _jsonOptions;
        private readonly MigrationManager _migrationManager;
        private bool _profileEventsSubscribed;

        private ModDataStore()
        {
            _jsonOptions = new()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                IncludeFields = false,
            };

            _migrationManager = new();
            ConfigureMigrations();
            RegisterDefaults();
        }

        public static ModDataStore Instance => _instance ??= new();

        /// <summary>
        ///     Whether global-scoped data has been initialized
        /// </summary>
        public bool IsInitialized => IsGlobalInitialized;

        public bool IsGlobalInitialized { get; private set; }
        public bool IsProfileInitialized { get; private set; }
        public bool HasProfileScopedEntries => _entries.Values.Any(e => e.Scope == SaveScope.Profile);

        /// <summary>
        ///     Initialize global-scoped data only (safe at early startup)
        /// </summary>
        public void InitializeGlobal()
        {
            if (IsGlobalInitialized) return;

            foreach (var entry in _entries.Values.Where(e => e.Scope == SaveScope.Global))
            {
                entry.Initialize(_jsonOptions, _migrationManager);
                entry.Load();
            }

            IsGlobalInitialized = true;
        }

        /// <summary>
        ///     Initialize profile-scoped data (must be called at safe profile path timing)
        /// </summary>
        public void InitializeProfileScoped()
        {
            if (IsProfileInitialized) return;

            if (!IsGlobalInitialized)
                InitializeGlobal();

            ProfileManager.Instance.Initialize();
            if (!_profileEventsSubscribed)
            {
                ProfileManager.Instance.ProfileChanged += OnProfileChanged;
                _profileEventsSubscribed = true;
            }

            foreach (var entry in _entries.Values.Where(e => e.Scope == SaveScope.Profile))
            {
                entry.Initialize(_jsonOptions, _migrationManager);
                entry.Load();
            }

            IsProfileInitialized = true;
        }

        /// <summary>
        ///     Initialize all scopes (compatibility helper)
        /// </summary>
        public void Initialize()
        {
            InitializeGlobal();
            InitializeProfileScoped();
        }

        public void Register<T>(
            string key,
            string fileName,
            SaveScope scope,
            Func<T>? defaultFactory = null,
            bool autoCreateIfMissing = false)
            where T : class, new()
        {
            if (_entries.ContainsKey(key))
                throw new InvalidOperationException($"Data key '{key}' is already registered.");

            var registration = new RegisteredDataEntry<T>(
                key,
                fileName,
                scope,
                defaultFactory ?? (() => new()),
                autoCreateIfMissing
            );

            _entries[key] = registration;

            if (!IsGlobalInitialized && scope == SaveScope.Global) return;
            if (!IsProfileInitialized && scope == SaveScope.Profile) return;
            registration.Initialize(_jsonOptions, _migrationManager);
            registration.Load();
        }

        public T Get<T>(string key) where T : class, new()
        {
            return GetEntry<T>(key).Data;
        }

        public void Modify<T>(string key, Action<T> modifier) where T : class, new()
        {
            GetEntry<T>(key).Modify(modifier);
        }

        public void Save(string key)
        {
            GetEntry(key).Save();
        }

        public bool HasExistingData(string key)
        {
            return GetEntry(key).HadExistingData;
        }

        public bool ReloadIfPathChanged()
        {
            if (!IsGlobalInitialized) return false;

            var reloaded = false;
            foreach (var entry in _entries.Values)
            {
                if (!entry.IsInitialized) continue;
                if (entry.ReloadIfPathChanged())
                    reloaded = true;
            }

            return reloaded;
        }

        /// <summary>
        ///     Save all data
        /// </summary>
        public void SaveAll()
        {
            foreach (var entry in _entries.Values)
                entry.Save();
        }

        private void OnProfileChanged(int oldProfileId, int newProfileId)
        {
            if (!IsProfileInitialized) return;

            Main.Logger.Info($"Profile changed from {oldProfileId} to {newProfileId}, handling data transition...");

            foreach (var entry in _entries.Values.Where(e => e.Scope == SaveScope.Profile))
            {
                entry.SaveToProfilePath(oldProfileId);
                entry.Load();
            }
        }

        private void RegisterDefaults()
        {
            Register(
                SettingsKey,
                Const.SettingsFileName,
                SaveScope.Global,
                () => new ModSettings(),
                true
            );
        }

        private void ConfigureMigrations()
        {
            _migrationManager.RegisterConfig<ModSettings>(
                ModSettings.CurrentDataVersion,
                1
            );
        }

        private IRegisteredDataEntry GetEntry(string key)
        {
            return !_entries.TryGetValue(key, out var entry)
                ? throw new KeyNotFoundException($"Data key '{key}' is not registered.")
                : entry;
        }

        private RegisteredDataEntry<T> GetEntry<T>(string key) where T : class, new()
        {
            var entry = GetEntry(key);
            if (entry is not RegisteredDataEntry<T> typed)
                throw new InvalidOperationException(
                    $"Data key '{key}' is registered as '{entry.DataType.Name}', not '{typeof(T).Name}'.");

            return typed;
        }

        private interface IRegisteredDataEntry
        {
            SaveScope Scope { get; }
            Type DataType { get; }
            bool HadExistingData { get; }
            bool IsInitialized { get; }
            void Initialize(JsonSerializerOptions jsonOptions, MigrationManager migrationManager);
            void Load();
            void Save();
            void SaveToProfilePath(int profileId);
            bool ReloadIfPathChanged();
        }

        private sealed class RegisteredDataEntry<T>(
            string key,
            string fileName,
            SaveScope scope,
            Func<T> defaultFactory,
            bool autoCreateIfMissing)
            : IRegisteredDataEntry where T : class, new()
        {
            private PersistentDataEntry<T>? _entry;
            private string? _lastLoadedPath;

            public T Data => _entry?.Data ?? throw new InvalidOperationException(
                $"Data entry '{key}' is not initialized.");

            public SaveScope Scope { get; } = scope;
            public Type DataType => typeof(T);
            public bool HadExistingData { get; private set; }
            public bool IsInitialized => _entry != null;

            public void Initialize(JsonSerializerOptions jsonOptions, MigrationManager migrationManager)
            {
                if (_entry != null) return;

                _entry = new(
                    fileName,
                    Scope,
                    defaultFactory(),
                    jsonOptions,
                    migrationManager,
                    autoCreateIfMissing
                );
            }

            public void Load()
            {
                if (_entry == null)
                    throw new InvalidOperationException($"Data entry '{key}' is not initialized.");

                var currentPath = ProfileManager.Instance.GetFilePath(fileName, Scope);
                _lastLoadedPath = currentPath;
                HadExistingData = FileOperations.FileExists(currentPath);
                _entry.Load();
            }

            public bool ReloadIfPathChanged()
            {
                if (_entry == null)
                    throw new InvalidOperationException($"Data entry '{key}' is not initialized.");

                var currentPath = ProfileManager.Instance.GetFilePath(fileName, Scope);
                if (string.Equals(_lastLoadedPath, currentPath, StringComparison.Ordinal))
                    return false;

                Main.Logger.Info(
                    $"Data path changed for '{key}': '{_lastLoadedPath ?? "<none>"}' -> '{currentPath}', reloading");
                Load();
                return true;
            }

            public void Save()
            {
                _entry?.Save();
            }

            public void SaveToProfilePath(int profileId)
            {
                if (_entry == null || Scope != SaveScope.Profile) return;

                var oldPath = ProfileManager.GetFilePath(fileName, Scope, profileId);
                _entry.SaveTo(oldPath);
            }

            public void Modify(Action<T> modifier)
            {
                if (_entry == null)
                    throw new InvalidOperationException($"Data entry '{key}' is not initialized.");

                _entry.Modify(modifier);
            }
        }
    }
}
