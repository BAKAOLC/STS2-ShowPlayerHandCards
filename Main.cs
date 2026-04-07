using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards.Patches;
using STS2ShowPlayerHandCards.Settings;
using STS2ShowPlayerHandCards.Utils;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;
using ModSettings = STS2ShowPlayerHandCards.Data.Models.ModSettings;

namespace STS2ShowPlayerHandCards
{
    [ModInitializer(nameof(Initialize))]
    public static class Main
    {
        public static readonly Logger Logger = RitsuLibFramework.CreateLogger(Const.ModId);

        public static bool IsModActive { get; private set; }

        public static void Initialize()
        {
            Logger.Info($"Mod ID: {Const.ModId}");
            Logger.Info($"Version: {Const.Version}");
            Logger.Info("Initializing mod...");

            try
            {
                var patcher = RitsuLibFramework.CreatePatcher(Const.ModId, "main");
                RegisterMainPatches(patcher);

                if (!RitsuLibFramework.ApplyRequiredPatcher(patcher, () => IsModActive = false))
                {
                    Logger.Error("Mod initialization failed: Critical patch(es) failed to apply");
                    return;
                }

                ModDataStore.Initialize();
                ModSettingsBootstrap.Initialize();
                InitializeData();
                InputHandler.EnsureExists();
                Logger.Info($"Press '{InputHandler.CurrentBinding}' to toggle hand card display visibility");

                IsModActive = true;
                Logger.Info("Mod initialization complete - Mod is now ACTIVE");
            }
            catch (Exception ex)
            {
                Logger.Error($"Mod initialization failed with exception: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
                IsModActive = false;
            }
        }

        private static void RegisterMainPatches(ModPatcher patcher)
        {
            patcher.RegisterPatch<CombatSetupPatch>();
            patcher.RegisterPatch<CardModelGetDescriptionEnergyVarRollbackPatch>();
        }

        private static void InitializeData()
        {
            var settings = ModDataStore.Get<ModSettings>(ModDataStore.SettingsKey);
            if (!InputHandler.TryNormalizeBinding(settings.ToggleKey, out var normalizedBinding))
            {
                normalizedBinding = InputHandler.DefaultToggleBinding;
                ModDataStore.Modify<ModSettings>(ModDataStore.SettingsKey,
                    s => s.ToggleKey = normalizedBinding);
                ModDataStore.Save(ModDataStore.SettingsKey);
                Logger.Warn(
                    $"Invalid toggle key in settings: '{settings.ToggleKey}', fallback to '{normalizedBinding}'.");
            }

            InputHandler.CurrentBinding = normalizedBinding;
        }
    }
}
