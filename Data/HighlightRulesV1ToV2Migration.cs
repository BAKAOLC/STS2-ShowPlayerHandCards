using System.Text.Json.Nodes;
using STS2RitsuLib.Utils.Persistence.Migration;

namespace STS2ShowPlayerHandCards.Data
{
    internal sealed class HighlightRulesV1ToV2Migration : IMigration
    {
        public int FromVersion => 1;

        public int ToVersion => 2;

        public bool Migrate(JsonObject data)
        {
            if (data["highlight_rules"] is JsonArray { Count: > 0 })
                return true;

            if (data["highlight_keywords"] is not JsonArray legacyKeywords || legacyKeywords.Count == 0)
                return true;

            var colorHex = data["highlight_color_hex"]?.GetValue<string>();
            var resolvedColor = string.IsNullOrWhiteSpace(colorHex) ? "#FFD740FF" : colorHex;
            var rules = new JsonArray();

            foreach (var node in legacyKeywords)
            {
                if (node is not JsonObject keywordObject)
                    continue;
                if (!keywordObject.TryGetPropertyValue("keyword", out var keywordNode) || keywordNode == null)
                    continue;
                var keyword = keywordNode.GetValue<string>().Trim();
                if (string.IsNullOrWhiteSpace(keyword))
                    continue;
                rules.Add(new JsonObject
                {
                    ["pattern"] = keyword,
                    ["color_hex"] = resolvedColor,
                    ["enabled"] = true,
                    ["match_mode"] = "Text",
                });
            }

            if (rules.Count > 0)
                data["highlight_rules"] = rules;

            return true;
        }
    }
}
