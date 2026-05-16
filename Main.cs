using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.RuntimeInput;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards.Patches;
using STS2ShowPlayerHandCards.Settings;
using STS2ShowPlayerHandCards.Utils;
using static STS2ShowPlayerHandCards.Settings.ModSettingsLocalization;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;
using ModSettings = STS2ShowPlayerHandCards.Data.Models.ModSettings;

namespace STS2ShowPlayerHandCards
{
    [ModInitializer(nameof(Initialize))]
    public static class Main
    {
        public static readonly Logger Logger = RitsuLibFramework.CreateLogger(Const.ModId);

        private static IRuntimeHotkeyHandle? _toggleHotkeyHandle;

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
                ApplyRuntimeHotkeysFromSettings();

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

        internal static void ApplyRuntimeHotkeysFromSettings()
        {
            var settings = ModDataStore.Get<ModSettings>(ModDataStore.SettingsKey);
            var originalBinding = settings.ToggleKey;
            var normalizedBinding =
                RuntimeHotkeyService.NormalizeOrDefault(originalBinding, InputHandler.DefaultToggleBinding);
            if (!string.Equals(originalBinding, normalizedBinding, StringComparison.Ordinal))
            {
                ModDataStore.Modify<ModSettings>(ModDataStore.SettingsKey, s => s.ToggleKey = normalizedBinding);
                ModDataStore.Save(ModDataStore.SettingsKey);
                Logger.Warn($"Invalid toggle key in settings: '{originalBinding}', fallback to '{normalizedBinding}'.");
            }

            if (_toggleHotkeyHandle == null)
            {
                _toggleHotkeyHandle = RuntimeHotkeyService.Register(normalizedBinding, ToggleHandCardDisplay,
                    new()
                    {
                        Id = "show-player-hand-cards.toggle-hand-display",
                        DisplayName = T("runtimeHotkey.toggle.displayName", "Toggle hand card display"),
                        Description = T("runtimeHotkey.toggle.description",
                            "Shows or hides the teammate hand card overlay."),
                        Purpose = "toggle-overlay",
                        Category = T("runtimeHotkey.category.gameplay", "Gameplay"),
                        DebugName = "show-player-hand-cards.toggle",
                    });
            }
            else if (!_toggleHotkeyHandle.TryRebind(normalizedBinding, out _))
            {
                _toggleHotkeyHandle.Dispose();
                _toggleHotkeyHandle = RuntimeHotkeyService.Register(normalizedBinding, ToggleHandCardDisplay,
                    new()
                    {
                        Id = "show-player-hand-cards.toggle-hand-display",
                        DisplayName = T("runtimeHotkey.toggle.displayName", "Toggle hand card display"),
                        Description = T("runtimeHotkey.toggle.description",
                            "Shows or hides the teammate hand card overlay."),
                        Purpose = "toggle-overlay",
                        Category = T("runtimeHotkey.category.gameplay", "Gameplay"),
                        DebugName = "show-player-hand-cards.toggle",
                    });
            }

            Logger.Info($"Press '{normalizedBinding}' to toggle hand card display visibility");
        }

        private static void ToggleHandCardDisplay()
        {
            HandCardDisplayService.ToggleVisibility();
            Logger.Info($"Hand card display toggled: {(HandCardDisplayService.IsHidden ? "Hidden" : "Visible")}");
        }
    }
}
