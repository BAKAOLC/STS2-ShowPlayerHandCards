using MegaCrit.Sts2.Core.Saves;
using STS2ShowPlayerHandCards.Patching.Models;

namespace STS2ShowPlayerHandCards.Utils.Persistence.Patches
{
    public class ProfileDeletePatch : IPatchMethod
    {
        public static string PatchId => "profile_delete";
        public static string Description => "Delete mod data when game profile is deleted";
        public static bool IsCritical => false;

        public static ModPatchTarget[] GetTargets()
        {
            return [new(typeof(SaveManager), "DeleteProfile", [typeof(int)])];
        }

        public static void Prefix(int profileId)
        {
            try
            {
                ProfileManager.DeleteProfileData(profileId);
            }
            catch (Exception ex)
            {
                PersistenceLog.Warn($"Failed to delete mod data for profile {profileId}: {ex.Message}");
            }
        }
    }
}
