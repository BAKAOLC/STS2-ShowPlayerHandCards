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
        ///     Whether the store has been initialized
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        ///     Initialize the data store
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;

            ProfileManager.Instance.Initialize();
            ProfileManager.Instance.ProfileChanged += OnProfileChanged;

            foreach (var entry in _entries.Values)
            {
                entry.Initialize(_jsonOptions, _migrationManager);
                entry.Load();
            }

            IsInitialized = true;
            Main.Logger.Info("ModDataStore initialized");
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

            if (!IsInitialized) return;
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
                1,
                1
            );
        }

        private IRegisteredDataEntry GetEntry(string key)
        {
            EnsureInitialized();

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

        private void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("ModDataStore is not initialized.");
        }

        private interface IRegisteredDataEntry
        {
            SaveScope Scope { get; }
            Type DataType { get; }
            bool HadExistingData { get; }
            void Initialize(JsonSerializerOptions jsonOptions, MigrationManager migrationManager);
            void Load();
            void Save();
            void SaveToProfilePath(int profileId);
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

            public T Data => _entry?.Data ?? throw new InvalidOperationException(
                $"Data entry '{key}' is not initialized.");

            public SaveScope Scope { get; } = scope;
            public Type DataType => typeof(T);
            public bool HadExistingData { get; private set; }

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
                HadExistingData = FileOperations.FileExists(currentPath);
                _entry.Load();
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
