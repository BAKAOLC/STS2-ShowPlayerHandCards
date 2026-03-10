using MegaCrit.Sts2.Core.Combat;
using STS2ShowPlayerHandCards.Patching.Models;
using STS2ShowPlayerHandCards.Utils;

namespace STS2ShowPlayerHandCards.Patches
{
    /// <summary>
    ///     Hooks into combat setup to initialize hand card display for teammates.
    /// </summary>
    public class CombatSetupPatch : IPatchMethod
    {
        public static string PatchId => "combat_setup_hand_display";
        public static string Description => "Initialize teammate hand card display on combat setup";

        public static ModPatchTarget[] GetTargets()
        {
            return
            [
                new(typeof(CombatManager), nameof(CombatManager.SetUpCombat)),
            ];
        }

        public static void Postfix()
        {
            HandCardDisplayService.EnsureSubscribed();
            HandCardDisplayService.SubscribeCurrentCombat();
            HandCardDisplayService.RefreshAll();
        }
    }
}
