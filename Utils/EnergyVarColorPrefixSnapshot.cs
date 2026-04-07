using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace STS2ShowPlayerHandCards.Utils
{
    internal static class EnergyVarColorPrefixSnapshot
    {
        public static List<(EnergyVar Var, string Prefix)> Capture(CardModel card)
        {
            var list = new List<(EnergyVar, string)>();
            CaptureSet(card.DynamicVars, list);
            if (card.Enchantment != null)
                CaptureSet(card.Enchantment.DynamicVars, list);
            return list;
        }

        public static void Restore(List<(EnergyVar Var, string Prefix)>? snapshot)
        {
            if (snapshot == null)
                return;
            foreach (var (v, p) in snapshot)
                v.ColorPrefix = p;
        }

        private static void CaptureSet(DynamicVarSet set, List<(EnergyVar, string)> list)
        {
            foreach (var dv in set.Values)
                if (dv is EnergyVar ev)
                    list.Add((ev, ev.ColorPrefix));
        }
    }
}
