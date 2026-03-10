using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace STS2ShowPlayerHandCards.Utils
{
    public class I18N : IDisposable
    {
        private readonly string _instanceName;
        private readonly string[] _pckFolders;
        private readonly string[] _resourceFolders;
        private bool _disposed;
        private string? _loadedLanguage;
        private bool _subscribed;
        private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);

        public I18N(string? instanceName = null, string[]? resourceFolders = null, string[]? pckFolders = null)
        {
            _instanceName = instanceName ?? "I18N";
            _resourceFolders = resourceFolders?.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray() ?? [];
            _pckFolders = pckFolders?.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray() ?? [];

            if (_resourceFolders.Length == 0 && _pckFolders.Length == 0)
                Main.Logger.Warn($"[{_instanceName}] Initialized with no translation sources");
            else
                Initialize();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            TryUnsubscribe();
            _translations.Clear();
            Changed = null;
            Main.Logger.Info($"[{_instanceName}] Instance disposed and resources released");
            GC.SuppressFinalize(this);
        }

        public event Action? Changed;

        public string Get(string key, string fallback)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureLoaded();
            return _translations.GetValueOrDefault(key) ?? fallback;
        }

        public void ForceReload()
        {
            var language = ResolveLanguage();
            _translations = LoadTranslations(language);
            _loadedLanguage = language;
            Main.Logger.Info(
                $"[{_instanceName}] Successfully reloaded translations for language '{language}' ({_translations.Count} entries)");
            BroadcastChange();
        }

        private void Initialize()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ForceReload();
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;

            try
            {
                var instance = LocManager.Instance;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (instance == null)
                {
                    Main.Logger.Debug(
                        $"[{_instanceName}] LocManager not available, will detect language changes lazily");
                    return;
                }

                instance.SubscribeToLocaleChange(OnLocaleChanged);
                _subscribed = true;
                Main.Logger.Info($"[{_instanceName}] Subscribed to locale change notifications");
            }
            catch (Exception ex)
            {
                Main.Logger.Warn(
                    $"[{_instanceName}] Unable to subscribe to locale changes, falling back to lazy detection: {ex.Message}");
            }
        }

        private void TryUnsubscribe()
        {
            if (!_subscribed) return;

            try
            {
                var instance = LocManager.Instance;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (instance == null) return;

                instance.UnsubscribeToLocaleChange(OnLocaleChanged);
                _subscribed = false;
                Main.Logger.Info(
                    $"[{_instanceName}] Successfully unsubscribed from locale change notifications");
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"[{_instanceName}] Error during locale change unsubscription: {ex.Message}");
            }
        }

        private void BroadcastChange()
        {
            Changed?.Invoke();
        }

        private void OnLocaleChanged()
        {
            if (_disposed) return;
            var language = ResolveLanguage();
            Main.Logger.Info($"[{_instanceName}] Locale change detected, switching to language: {language}");
            _loadedLanguage = null;
            ForceReload();
        }

        private void EnsureLoaded()
        {
            if (!_subscribed) TrySubscribe();

            var language = ResolveLanguage();
            if (string.Equals(_loadedLanguage, language, StringComparison.OrdinalIgnoreCase)) return;

            _translations = LoadTranslations(language);
            _loadedLanguage = language;
            Main.Logger.Info(
                $"[{_instanceName}] Successfully loaded translations for language '{_loadedLanguage}' ({_translations.Count} entries)");
        }

        private Dictionary<string, string> LoadTranslations(string language)
        {
            var candidates = GetLanguageCandidates(language);
            var enumerable = candidates as string[] ?? candidates.ToArray();
            foreach (var candidate in enumerable)
            {
                // Try embedded resources first
                var resourceSpan = _resourceFolders.AsSpan();
                foreach (var res in resourceSpan)
                {
                    var dictionary = TryLoadEmbedded(res, candidate);
                    if (dictionary is not { Count: > 0 }) continue;
                    Main.Logger.Info(
                        $"[{_instanceName}] Loaded translations from embedded resource: {res}.{candidate}.json ({dictionary.Count} entries)");
                    return dictionary;
                }

                // Try PCK files as fallback
                var pckSpan = _pckFolders.AsSpan();
                foreach (var res in pckSpan)
                {
                    var path = $"{res}/{candidate}.json";
                    var dictionary = TryLoadFromPck(path);
                    if (dictionary is not { Count: > 0 }) continue;
                    Main.Logger.Info(
                        $"[{_instanceName}] Loaded translations from PCK file: {path} ({dictionary.Count} entries)");
                    return dictionary;
                }
            }

            Main.Logger.Warn(
                $"[{_instanceName}] No translation files found for language '{language}' (tried {string.Join(", ", enumerable)})");
            return new(StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, string>? TryLoadEmbedded(string resourceFolder, string language)
        {
            var resourceName = $"{resourceFolder}.{language}.json";

            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Main.Logger.Debug($"[{_instanceName}] Embedded resource not found: '{resourceName}'");
                    return null;
                }

                var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);

                if (translations != null) return translations;
                Main.Logger.Error(
                    $"[{_instanceName}] Deserialization resulted in null object for embedded resource '{resourceName}'");
                return null;
            }
            catch (JsonException ex)
            {
                Main.Logger.Error(
                    $"[{_instanceName}] JSON parsing error in embedded resource '{resourceName}': {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Main.Logger.Error(
                    $"[{_instanceName}] Unexpected error loading embedded resource '{resourceName}': {ex.Message}");
                return null;
            }
        }


        private Dictionary<string, string>? TryLoadFromPck(string path)
        {
            var result = FileOperations.ReadJson<Dictionary<string, string>>(path, null, _instanceName);
            if (!result.Success || result.Data == null) return null;
            return result.Data;
        }

        private static string ResolveLanguage()
        {
            string? language = null;
            try
            {
                var instance = LocManager.Instance;
                // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                language = instance?.Language;
            }
            catch
            {
                // Silently ignore LocManager access errors
            }

            if (!string.IsNullOrWhiteSpace(language)) return NormalizeLanguageCode(language);

            try
            {
                language = TranslationServer.GetLocale();
            }
            catch
            {
                // Silently ignore TranslationServer access errors
            }

            return NormalizeLanguageCode(language);
        }

        private static IEnumerable<string> GetLanguageCandidates(string language)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Try exact match first
            if (seen.Add(language)) yield return language;

            // Try base language (e.g., "en" from "en_US")
            var separatorIndex = language.IndexOf('_');
            if (separatorIndex > 0)
            {
                var baseLanguage = language[..separatorIndex];
                if (seen.Add(baseLanguage)) yield return baseLanguage;
            }

            // Special handling for Chinese variants
            if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) && seen.Add("zhs"))
                yield return "zhs";

            // Fallback to English variants
            if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase) && seen.Add("en"))
                yield return "en";

            // Final fallback to English
            if (seen.Add("en")) yield return "en";
        }

        private static string NormalizeLanguageCode(string? language)
        {
            if (string.IsNullOrWhiteSpace(language)) return "en";
            var text = language.Trim().Replace('-', '_').ToLowerInvariant();
            return text switch
            {
                "zh_cn" or "zh_hans" or "zh_sg" or "zh" => "zhs",
                "en_us" or "en_gb" => "en",
                _ => text,
            };
        }
    }
}
