namespace STS2ShowPlayerHandCards.Utils.Persistence
{
    internal static class PersistenceLog
    {
        private const string Prefix = "[Persistence]";

        public static void Debug(string message)
        {
            Main.Logger.Debug($"{Prefix} {message}");
        }

        public static void Info(string message)
        {
            Main.Logger.Info($"{Prefix} {message}");
        }

        public static void Warn(string message)
        {
            Main.Logger.Warn($"{Prefix} {message}");
        }

        public static void Error(string message)
        {
            Main.Logger.Error($"{Prefix} {message}");
        }
    }
}
