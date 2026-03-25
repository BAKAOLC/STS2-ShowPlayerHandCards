using System.Reflection;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;

namespace STS2ShowPlayerHandCards.Settings
{
    internal static class ModSettingsLocalization
    {
        private static readonly Lazy<I18N> InstanceFactory = new(() => new(
            "STS2-ShowPlayerHandCards-ModSettings",
            resourceFolders: ["STS2ShowPlayerHandCards.Settings.Localization.ModSettings"],
            resourceAssembly: Assembly.GetExecutingAssembly()));

        public static I18N Instance => InstanceFactory.Value;

        public static ModSettingsText T(string key, string fallback)
        {
            return ModSettingsText.I18N(Instance, key, fallback);
        }

        public static string Get(string key, string fallback)
        {
            return Instance.Get(key, fallback);
        }
    }
}
