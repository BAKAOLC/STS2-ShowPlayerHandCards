using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards.Data.Models;

namespace STS2ShowPlayerHandCards.Utils
{
    internal static class HandCardDisplaySettings
    {
        private const float BaseMiniCardScale = 0.065f;
        private const float BaseCardYOffset = 4f;
        private const float BaseCardSpacing = 1f;

        public static float GetMiniCardScale()
        {
            var scale = Math.Clamp(GetSettings().ContentScale, ModSettings.MinContentScale,
                ModSettings.MaxContentScale);
            return BaseMiniCardScale * (float)scale;
        }

        public static Vector2 GetScaledCardSize()
        {
            return NCard.defaultSize * GetMiniCardScale();
        }

        public static float GetCardSpacing()
        {
            var scale = Math.Clamp(GetSettings().ContentScale, ModSettings.MinContentScale,
                ModSettings.MaxContentScale);
            return Mathf.Max(BaseCardSpacing, Mathf.Round(BaseCardSpacing * Mathf.Sqrt((float)scale)));
        }

        public static Vector2 GetAutoOffset()
        {
            var scaledSize = GetScaledCardSize();
            var baseHeight = NCard.defaultSize.Y * BaseMiniCardScale;
            var extraHeight = Mathf.Max(0f, scaledSize.Y - baseHeight);
            return new(0f, BaseCardYOffset - extraHeight * 0.18f);
        }

        public static Vector2 GetUserOffset()
        {
            var settings = GetSettings();
            return new((float)settings.PositionOffsetX, (float)settings.PositionOffsetY);
        }

        public static float GetContentWidth(int count)
        {
            if (count <= 0)
                return 0f;
            var cardWidth = GetScaledCardSize().X;
            return count * cardWidth + (count - 1) * GetCardSpacing();
        }

        public static bool TryGetHighlightColor(CardModel card, out Color color)
        {
            foreach (var rule in GetRules())
            {
                var validation = ValidateRule(rule);
                if (!validation.IsValid)
                    continue;
                if (!MatchesRule(rule, card)) continue;
                color = GetRuleColor(rule.ColorHex);
                return true;
            }

            color = default;
            return false;
        }

        public static RuleValidationResult ValidateRule(HighlightRuleEntry rule)
        {
            return rule.MatchMode switch
            {
                HighlightMatchMode.Regex => ValidateRegex(rule.Pattern),
                HighlightMatchMode.Template => ValidateCardTemplate(rule),
                _ => string.IsNullOrWhiteSpace(rule.Pattern)
                    ? RuleValidationResult.Invalid("rule.validation.pattern_required")
                    : RuleValidationResult.Valid(),
            };
        }

        public static Color GetRuleColor(string? colorHex)
        {
            return TryParseHexColor(colorHex, out var parsed) ? parsed : GetDefaultHighlightColor();
        }

        public static Color GetDefaultHighlightColor()
        {
            return TryParseHexColor(GetSettings().HighlightColorHex, out var parsed)
                ? parsed
                : new(1f, 215f / 255f, 64f / 255f);
        }

        private static IEnumerable<HighlightRuleEntry> GetRules()
        {
            return GetSettings().HighlightRules
                .Where(rule => rule.Enabled);
        }

        private static bool MatchesRule(HighlightRuleEntry rule, CardModel card)
        {
            return rule.MatchMode switch
            {
                HighlightMatchMode.Regex => GetNormalizedCandidates(card).Any(text =>
                    Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
                HighlightMatchMode.Template => MatchesTemplateRule(rule, card),
                _ => GetNormalizedCandidates(card).Any(text =>
                    text.Contains(NormalizeForMatch(rule.Pattern), StringComparison.OrdinalIgnoreCase)),
            };
        }

        private static bool MatchesTemplateRule(HighlightRuleEntry rule, CardModel card)
        {
            if (rule.Keywords.Count > 0 && !rule.Keywords.All(required =>
                    card.CanonicalKeywords.Any(keyword =>
                        string.Equals(keyword.ToString(), required, StringComparison.OrdinalIgnoreCase))))
                return false;
            if (rule.Types.Count > 0 && !rule.Types.Any(type =>
                    string.Equals(type, card.Type.ToString(), StringComparison.OrdinalIgnoreCase)))
                return false;
            if (rule.Rarities.Count > 0 && !rule.Rarities.Any(rarity =>
                    string.Equals(rarity, card.Rarity.ToString(), StringComparison.OrdinalIgnoreCase)))
                return false;
            if (rule.TargetTypes.Count > 0 && !rule.TargetTypes.Any(target =>
                    string.Equals(target, card.TargetType.ToString(), StringComparison.OrdinalIgnoreCase)))
                return false;
            if (rule.RequireUpgraded.HasValue && card.IsUpgraded != rule.RequireUpgraded.Value)
                return false;
            if (rule.RequirePlayable.HasValue && card.CanPlay() != rule.RequirePlayable.Value)
                return false;
            if (rule.EffectTerms.Count <= 0) return true;
            var candidates = GetNormalizedCandidates(card).ToArray();
            return rule.EffectTerms.All(term =>
                candidates.Any(text =>
                    text.Contains(NormalizeForMatch(term), StringComparison.OrdinalIgnoreCase)));
        }

        private static RuleValidationResult ValidateRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return RuleValidationResult.Invalid("rule.validation.pattern_required");
            try
            {
                _ = Regex.Match(string.Empty, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                return RuleValidationResult.Valid();
            }
            catch (ArgumentException ex)
            {
                return RuleValidationResult.Invalid("rule.validation.regex_invalid", ex.Message);
            }
        }

        private static RuleValidationResult ValidateCardTemplate(HighlightRuleEntry rule)
        {
            var hasCondition = rule.Keywords.Count > 0 || rule.Types.Count > 0 || rule.Rarities.Count > 0 ||
                               rule.TargetTypes.Count > 0 || rule.EffectTerms.Count > 0 ||
                               rule.RequireUpgraded.HasValue || rule.RequirePlayable.HasValue;
            return hasCondition
                ? RuleValidationResult.Valid()
                : RuleValidationResult.Invalid("rule.validation.template_required");
        }

        private static IEnumerable<string> GetNormalizedCandidates(CardModel card)
        {
            return card.CanonicalKeywords.Select(keyword => keyword.ToString())
                .Concat(card.HoverTips.SelectMany(GetTexts))
                .Select(NormalizeForMatch)
                .Where(text => !string.IsNullOrWhiteSpace(text));
        }

        private static string NormalizeForMatch(string text)
        {
            var withoutBbCode = text.StripBbCode();
            var withoutHtml = NSearchBar.RemoveHtmlTags(withoutBbCode);
            return NSearchBar.Normalize(withoutHtml);
        }

        private static IEnumerable<string> GetTexts(IHoverTip hoverTip)
        {
            if (hoverTip is not HoverTip concrete) yield break;
            if (!string.IsNullOrWhiteSpace(concrete.Title))
                yield return concrete.Title;
            if (!string.IsNullOrWhiteSpace(concrete.Description))
                yield return concrete.Description;
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

            color = new(
                Convert.ToByte(hex[..2], 16) / 255f,
                Convert.ToByte(hex[2..4], 16) / 255f,
                Convert.ToByte(hex[4..6], 16) / 255f,
                Convert.ToByte(hex[6..8], 16) / 255f);
            return true;
        }
    }

    internal readonly record struct RuleValidationResult(bool IsValid, string LocalizationKey, string? Detail)
    {
        public static RuleValidationResult Valid()
        {
            return new(true, string.Empty, null);
        }

        public static RuleValidationResult Invalid(string localizationKey, string? detail = null)
        {
            return new(false, localizationKey, detail);
        }
    }
}
