using System.Text.Json.Nodes;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace STS2ShowPlayerHandCards.Data
{
    internal sealed class LayoutSettingsV2ToV3Migration : IMigration
    {
        public int FromVersion => 2;

        public int ToVersion => 3;

        public bool Migrate(JsonObject data)
        {
            data.TryAdd("manual_positioning_enabled", false);
            data.TryAdd("reserve_original_width", true);
            data.TryAdd("slot_offsets", new JsonArray());
            return true;
        }
    }
}
