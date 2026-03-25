using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards.Data.Models;

namespace STS2ShowPlayerHandCards.Utils
{
    internal static class HandCardDisplaySettings
    {
        private const float BaseMiniCardScale = 0.065f;

        public static float GetMiniCardScale()
        {
            return BaseMiniCardScale * Mathf.Clamp(GetSettings().ContentScale, 0.5f, 2.0f);
        }

        public static Vector2 GetScaledCardSize()
        {
            return NCard.defaultSize * GetMiniCardScale();
        }

        public static Color GetHighlightColor()
        {
            var settings = GetSettings();
            if (TryParseHexColor(settings.HighlightColorHex, out var parsed))
                return parsed;

            return new Color(1f, 215f / 255f, 64f / 255f, 1f);
        }

        public static bool ShouldHighlight(CardModel card)
        {
            var keywords = GetSettings().HighlightKeywords
                .Select(entry => entry.Keyword.Trim())
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (keywords.Length == 0)
                return false;

            var candidates = card.Keywords.Select(keyword => keyword.ToString())
                .Concat(card.HoverTips.SelectMany(GetTexts))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();

            return keywords.Any(keyword =>
                candidates.Any(text => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        private static IEnumerable<string> GetTexts(IHoverTip hoverTip)
        {
            if (hoverTip is HoverTip concrete)
            {
                if (!string.IsNullOrWhiteSpace(concrete.Title))
                    yield return concrete.Title;
                if (!string.IsNullOrWhiteSpace(concrete.Description))
                    yield return concrete.Description;
            }
        }

        private static ModSettings GetSettings()
        {
            return ModDataStore.Get<ModSettings>(ModDataStore.SettingsKey);
        }

        private static bool TryParseHexColor(string? text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (!trimmed.StartsWith('#'))
                trimmed = $"#{trimmed}";

            var hex = trimmed[1..];
            if (hex.Length is not (3 or 4 or 6 or 8) || hex.Any(c => !Uri.IsHexDigit(c)))
                return false;
            if (hex.Length is 3 or 4)
                hex = string.Concat(hex.Select(c => new string(c, 2)));
            if (hex.Length == 6)
                hex += "FF";

            color = new Color(
                Convert.ToByte(hex[0..2], 16) / 255f,
                Convert.ToByte(hex[2..4], 16) / 255f,
                Convert.ToByte(hex[4..6], 16) / 255f,
                Convert.ToByte(hex[6..8], 16) / 255f);
            return true;
        }
    }
}
