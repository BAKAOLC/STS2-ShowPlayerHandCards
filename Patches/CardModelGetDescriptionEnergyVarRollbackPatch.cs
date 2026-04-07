using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Models;
using STS2ShowPlayerHandCards.Utils;

namespace STS2ShowPlayerHandCards.Patches
{
    public sealed class CardModelGetDescriptionEnergyVarRollbackPatch : IPatchMethod
    {
        public static string PatchId => "card_model_get_description_energy_var_prefix_rollback";

        public static string Description =>
            "Restores EnergyVar.ColorPrefix after GetDescriptionForPile so UI text building does not leak into multiplayer checksum state.";

        public static ModPatchTarget[] GetTargets()
        {
            return
            [
                new(typeof(CardModel), nameof(CardModel.GetDescriptionForPile), [typeof(PileType), typeof(Creature)]),
                new(typeof(CardModel), nameof(CardModel.GetDescriptionForUpgradePreview), []),
            ];
        }

        // ReSharper disable InconsistentNaming
        public static void Prefix(CardModel __instance, ref List<(EnergyVar Var, string Prefix)>? __state)
            // ReSharper restore InconsistentNaming
        {
            __state = EnergyVarColorPrefixSnapshot.Capture(__instance);
        }

        // ReSharper disable once InconsistentNaming
        public static Exception? Finalizer(ref List<(EnergyVar Var, string Prefix)>? __state)
        {
            EnergyVarColorPrefixSnapshot.Restore(__state);
            __state = null;
            return null;
        }
    }
}
