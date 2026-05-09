using System.Text.Json.Nodes;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace STS2ShowPlayerHandCards.Data
{
    internal sealed class LayoutSettingsV3ToV4Migration : IMigration
    {
        public int FromVersion => 3;

        public int ToVersion => 4;

        public bool Migrate(JsonObject data)
        {
            data["slot_offsets"] = new JsonArray();
            return true;
        }
    }
}
