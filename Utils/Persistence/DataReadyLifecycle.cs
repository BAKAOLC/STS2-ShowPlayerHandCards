using STS2ShowPlayerHandCards.Data;

namespace STS2ShowPlayerHandCards.Utils.Persistence
{
    /// <summary>
    ///     Unified lifecycle API: notifies when profile-scoped mod data path is ready and data has been synchronized.
    ///     Other modules can subscribe to DataReady or queue work through RunWhenReady.
    /// </summary>
    public static class DataReadyLifecycle
    {
        private static readonly List<Action<int>> PendingCallbacks = [];

        public static bool IsReady { get; private set; }
        public static int ReadyProfileId { get; private set; } = -1;

        public static event Action<int, string>? DataReady;

        public static void RunWhenReady(Action<int> action)
        {
            if (IsReady)
            {
                action(ReadyProfileId);
                return;
            }

            PendingCallbacks.Add(action);
        }

        public static void NotifyPotentialReady(string source)
        {
            try
            {
                ProfileManager.Instance.RefreshCurrentProfile();

                Main.EnsureProfileServicesInitialized();

                if (!ModDataStore.Instance.IsProfileInitialized)
                    return;

                ModDataStore.Instance.ReloadIfPathChanged();

                var profileId = ProfileManager.Instance.CurrentProfileId;
                var changed = !IsReady || ReadyProfileId != profileId;

                IsReady = true;
                ReadyProfileId = profileId;

                if (!changed)
                    return;

                if (ModDataStore.Instance.HasProfileScopedEntries)
                    Main.Logger.Info($"Data ready for profile {profileId} ({source})");
                DataReady?.Invoke(profileId, source);

                if (PendingCallbacks.Count == 0) return;

                var callbacks = PendingCallbacks.ToArray();
                PendingCallbacks.Clear();

                foreach (var callback in callbacks)
                    callback(profileId);
            }
            catch (Exception ex)
            {
                PersistenceLog.Warn($"Failed to notify data ready lifecycle from '{source}': {ex.Message}");
            }
        }
    }
}
