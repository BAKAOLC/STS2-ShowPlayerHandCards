using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;
using STS2ShowPlayerHandCards.Data.Models;

namespace STS2ShowPlayerHandCards.Data
{
    public class ModDataStore
    {
        public const string SettingsKey = "settings";

        private static readonly STS2RitsuLib.Data.ModDataStore Store =
            STS2RitsuLib.Data.ModDataStore.For(Const.ModId);

        public static void Initialize()
        {
            using (RitsuLibFramework.BeginModDataRegistration(Const.ModId))
            {
                Store.Register<ModSettings>(
                    SettingsKey,
                    Const.SettingsFileName,
                    SaveScope.Global,
                    () => new(),
                    true,
                    new()
                    {
                        CurrentDataVersion = ModSettings.CurrentDataVersion,
                        MinimumSupportedDataVersion = 1,
                        SchemaVersionProperty = "data_version",
                    });
            }
        }

        public static T Get<T>(string key) where T : class, new()
        {
            return Store.Get<T>(key);
        }

        public static void Modify<T>(string key, Action<T> modifier) where T : class, new()
        {
            Store.Modify(key, modifier);
        }

        public static void Save(string key)
        {
            Store.Save(key);
        }
    }
}
