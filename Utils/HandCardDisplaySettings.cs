using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards.Data.Models;

namespace STS2ShowPlayerHandCards.Utils
{
    internal static class HandCardDisplaySettings
    {
        private const float BaseMiniCardScale = 0.065f;
        private const float BaseCardYOffset = 4f;
        private const float BaseCardSpacing = 1f;
        private const float AutoGap = 8f;
        private const float ExtraAvoidanceShift = 12f;

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

        public static Vector2 GetLegacyAutoOffset()
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

        public static bool IsManualPositioningEnabled()
        {
            return GetSettings().ManualPositioningEnabled;
        }

        public static bool ShouldReserveOriginalWidth()
        {
            return GetSettings().ReserveOriginalWidth;
        }

        public static Vector2 GetSlotOffset(int slotIndex)
        {
            var entry = GetSettings().SlotOffsets.FirstOrDefault(item => item.SlotIndex == slotIndex);
            return entry == null ? Vector2.Zero : new((float)entry.OffsetX, (float)entry.OffsetY);
        }

        public static void SetSlotOffset(int slotIndex, Vector2 offset)
        {
            ModDataStore.Modify<ModSettings>(ModDataStore.SettingsKey, settings =>
            {
                var entry = settings.SlotOffsets.FirstOrDefault(item => item.SlotIndex == slotIndex);
                if (entry == null)
                {
                    settings.SlotOffsets.Add(new()
                    {
                        SlotIndex = slotIndex,
                        OffsetX = offset.X,
                        OffsetY = offset.Y,
                    });
                    return;
                }

                entry.OffsetX = offset.X;
                entry.OffsetY = offset.Y;
            });
            ModDataStore.Save(ModDataStore.SettingsKey);
        }

        public static void ClearSlotOffsets()
        {
            ModDataStore.Modify<ModSettings>(ModDataStore.SettingsKey, settings => settings.SlotOffsets.Clear());
            ModDataStore.Save(ModDataStore.SettingsKey);
        }

        public static float GetContentWidth(int count)
        {
            if (count <= 0)
                return 0f;
            var cardWidth = GetScaledCardSize().X;
            return count * cardWidth + (count - 1) * GetCardSpacing();
        }

        public static Vector2 ResolveAutoPosition(Rect2 anchorRect, Vector2 contentSize,
            IReadOnlyList<Rect2> avoidRects,
            Rect2 viewportRect)
        {
            var preferredYOffset = GetLegacyAutoOffset().Y;
            var candidates = new[]
            {
                new Vector2(anchorRect.End.X + AutoGap, anchorRect.Position.Y + preferredYOffset),
                new Vector2(anchorRect.Position.X, anchorRect.End.Y + AutoGap),
                new Vector2(anchorRect.Position.X, anchorRect.Position.Y - contentSize.Y - AutoGap),
                new Vector2(anchorRect.Position.X - contentSize.X - AutoGap, anchorRect.Position.Y + preferredYOffset),
                new Vector2(anchorRect.End.X + AutoGap, anchorRect.Position.Y + preferredYOffset + ExtraAvoidanceShift),
            };

            return candidates
                .OrderBy(candidate => ScoreCandidate(new(candidate, contentSize), avoidRects, viewportRect))
                .FirstOrDefault();
        }

        public static bool TryGetHighlightColor(CardModel card, out Color color)
        {
            return HighlightEvaluator.TryGet(card, out color);
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

        private static float ScoreCandidate(Rect2 candidateRect, IReadOnlyList<Rect2> avoidRects, Rect2 viewportRect)
        {
            var penalty = 0f;
            foreach (var avoidRect in avoidRects)
            {
                var overlap = candidateRect.Intersection(avoidRect);
                penalty += overlap.Size.X * overlap.Size.Y;
            }

            if (!viewportRect.Encloses(candidateRect))
            {
                var overflowLeft = Mathf.Max(0f, viewportRect.Position.X - candidateRect.Position.X);
                var overflowTop = Mathf.Max(0f, viewportRect.Position.Y - candidateRect.Position.Y);
                var overflowRight = Mathf.Max(0f, candidateRect.End.X - viewportRect.End.X);
                var overflowBottom = Mathf.Max(0f, candidateRect.End.Y - viewportRect.End.Y);
                penalty += (overflowLeft + overflowTop + overflowRight + overflowBottom) * 1000f;
            }

            return penalty;
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
