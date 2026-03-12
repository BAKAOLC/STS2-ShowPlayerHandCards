using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace STS2ShowPlayerHandCards.Utils
{
    public class I18N : IDisposable
    {
        private readonly string[] _fsFolders;
        private readonly string _instanceName;
        private readonly string[] _pckFolders;
        private readonly string[] _resourceFolders;
        private bool _disposed;
        private string? _loadedLanguage;
        private bool _subscribed;
        private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);

        public I18N(string? instanceName = null,
            string[]? fsFolders = null,
            string[]? resourceFolders = null,
            string[]? pckFolders = null)
        {
            _instanceName = instanceName ?? "I18N";
            _resourceFolders = resourceFolders?.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray() ?? [];
            _fsFolders = fsFolders?.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray() ?? [];
            _pckFolders = pckFolders?.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray() ?? [];

            if (_resourceFolders.Length == 0 && _fsFolders.Length == 0 && _pckFolders.Length == 0)
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
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var sourceCount = 0;

            foreach (var folder in _fsFolders)
            {
                var path = $"{folder}/{language}.json";
                var dictionary = TryLoadFromFileSystem(path);
                if (dictionary is not { Count: > 0 }) continue;

                var newKeys = dictionary.Count(kvp => merged.TryAdd(kvp.Key, kvp.Value));
                sourceCount++;
                Main.Logger.Info(
                    $"[{_instanceName}] Merged from FS: {path} ({dictionary.Count} entries, {newKeys} new)");
            }

            foreach (var res in _resourceFolders)
            {
                var dictionary = TryLoadEmbedded(res, language);
                if (dictionary is not { Count: > 0 }) continue;

                var newKeys = dictionary.Count(kvp => merged.TryAdd(kvp.Key, kvp.Value));
                sourceCount++;
                Main.Logger.Info(
                    $"[{_instanceName}] Merged from embedded: {res}.{language}.json ({dictionary.Count} entries, {newKeys} new)");
            }

            foreach (var res in _pckFolders)
            {
                var path = $"{res}/{language}.json";
                var dictionary = TryLoadFromPck(path);
                if (dictionary is not { Count: > 0 }) continue;

                var newKeys = dictionary.Count(kvp => merged.TryAdd(kvp.Key, kvp.Value));
                sourceCount++;
                Main.Logger.Info(
                    $"[{_instanceName}] Merged from PCK: {path} ({dictionary.Count} entries, {newKeys} new)");
            }

            if (merged.Count == 0)
                Main.Logger.Warn($"[{_instanceName}] No translations found for '{language}'");
            else
                Main.Logger.Info($"[{_instanceName}] Total: {merged.Count} entries from {sourceCount} source(s)");

            return merged;
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

        private Dictionary<string, string>? TryLoadFromFileSystem(string path)
        {
            if (!FileOperations.FileExists(path))
            {
                Main.Logger.Debug($"[{_instanceName}] FS file not found: '{path}'");
                return null;
            }

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

        private static string NormalizeLanguageCode(string? language)
        {
            if (string.IsNullOrWhiteSpace(language)) return "eng";
            var text = language.Trim().Replace('-', '_').ToLowerInvariant();
            return text switch
            {
                "zh_cn" or "zh_hans" or "zh_sg" or "zh" => "zhs",
                "en_us" or "en_gb" or "en" or "eng" => "eng",
                "ja" or "ja_jp" or "jpn" => "jpn",
                "ko" or "ko_kr" or "kor" => "kor",
                "de" or "de_de" or "deu" => "deu",
                "es" or "es_es" or "esp" => "esp",
                "fr" or "fr_fr" or "fra" => "fra",
                "it" or "it_it" or "ita" => "ita",
                "pl" or "pl_pl" or "pol" => "pol",
                "pt" or "pt_br" or "ptb" => "ptb",
                "ru" or "ru_ru" or "rus" => "rus",
                "th" or "th_th" or "tha" => "tha",
                "tr" or "tr_tr" or "tur" => "tur",
                _ => text,
            };
        }
    }
}
