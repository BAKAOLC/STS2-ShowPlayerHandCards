using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards.Patches;
using STS2ShowPlayerHandCards.Patching.Core;
using STS2ShowPlayerHandCards.Utils;
using STS2ShowPlayerHandCards.Utils.Persistence.Patches;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;
using ModSettings = STS2ShowPlayerHandCards.Data.Models.ModSettings;

namespace STS2ShowPlayerHandCards
{
    [ModInitializer("Initialize")]
    public static class Main
    {
        public static readonly Logger Logger = new(Const.ModId, LogType.Generic);
        private static readonly Dictionary<string, ModPatcher> Patchers = [];

        public static bool IsModActive { get; private set; }

        public static void Initialize()
        {
            Logger.Info($"Mod ID: {Const.ModId}");
            Logger.Info($"Version: {Const.Version}");
            Logger.Info("Initializing mod...");

            try
            {
                InitializeData();

                var frameworkPatcher = GetOrCreatePatcher("framework", "Framework-level patches");
                RegisterFrameworkPatches(frameworkPatcher);

                var mainPatcher = GetOrCreatePatcher("main", "Main patches");
                RegisterMainPatches(mainPatcher);
                var allSuccess = ApplyAllPatchers();

                if (!allSuccess)
                {
                    Logger.Error("Mod initialization failed: Critical patch(es) failed to apply");
                    Logger.Error("Mod is in a failed state and will not be active. Please check the logs for details.");
                    IsModActive = false;
                    return;
                }

                IsModActive = true;
                Logger.Info("Mod initialization complete - Mod is now ACTIVE");
                LogPatcherStatus();

                InputHandler.EnsureExists();
                Logger.Info($"Press '{InputHandler.CurrentKey}' to toggle hand card display visibility");
            }
            catch (Exception ex)
            {
                Logger.Error($"Mod initialization failed with exception: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
                IsModActive = false;
            }
        }

        private static ModPatcher GetOrCreatePatcher(string patcherName, string description)
        {
            var patcherId = $"{Const.ModId}.{patcherName}";

            if (Patchers.TryGetValue(patcherName, out var createPatcher)) return createPatcher;

            Logger.Info($"Creating patcher: {patcherName} - {description}");
            var patcher = new ModPatcher(patcherId, Logger, patcherName);
            Patchers[patcherName] = patcher;
            return patcher;
        }

        private static bool ApplyAllPatchers()
        {
            var allSuccess = true;

            foreach (var (name, patcher) in Patchers)
            {
                Logger.Info($"Applying patcher: {name}");
                var success = patcher.PatchAll();

                if (success) continue;
                Logger.Error($"Patcher '{name}' failed to apply");
                allSuccess = false;

                Logger.Error("Rolling back all patchers due to critical failure...");
                UnpatchAll();
                break;
            }

            return allSuccess;
        }

        private static void UnpatchAll()
        {
            foreach (var (name, patcher) in Patchers)
            {
                Logger.Info($"Unpatching: {name}");
                patcher.UnpatchAll();
            }

            IsModActive = false;
        }

        private static void LogPatcherStatus()
        {
            Logger.Info("=== Patcher Status ===");
            foreach (var (name, patcher) in Patchers)
                Logger.Info(
                    $"  {name}: {patcher.AppliedPatchCount}/{patcher.RegisteredPatchCount} patches applied (Applied: {patcher.IsApplied})");
            Logger.Info("======================");
        }

        private static void RegisterFrameworkPatches(ModPatcher patcher)
        {
            patcher.RegisterPatch<ProfileDeletePatch>();
        }

        private static void RegisterMainPatches(ModPatcher patcher)
        {
            patcher.RegisterPatch<CombatSetupPatch>();
        }

        private static void InitializeData()
        {
            ModDataStore.Instance.Initialize();

            var settings = ModDataStore.Instance.Get<ModSettings>(ModDataStore.SettingsKey);
            if (!Enum.TryParse<Key>(settings.ToggleKey, true, out var toggleKey))
            {
                toggleKey = InputHandler.DefaultToggleKey;
                ModDataStore.Instance.Modify<ModSettings>(ModDataStore.SettingsKey,
                    s => s.ToggleKey = toggleKey.ToString());
                ModDataStore.Instance.Save(ModDataStore.SettingsKey);
                Logger.Warn($"Invalid toggle key in settings: '{settings.ToggleKey}', fallback to '{toggleKey}'.");
            }

            InputHandler.CurrentKey = toggleKey;
        }
    }
}
