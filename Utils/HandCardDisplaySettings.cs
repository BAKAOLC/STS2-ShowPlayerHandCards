using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards.Data.Models;

namespace STS2ShowPlayerHandCards.Utils
{
    internal static class HandCardDisplaySettings
    {
        private const float AutoGap = 8f;
        private const float ExtraAvoidanceShift = 12f;

        public static float GetMiniCardScale()
        {
            return LayoutSettingsSnapshot.Current.MiniCardScale;
        }

        public static Vector2 GetScaledCardSize()
        {
            return LayoutSettingsSnapshot.Current.ScaledCardSize;
        }

        public static float GetCardSpacing()
        {
            return LayoutSettingsSnapshot.Current.CardSpacing;
        }

        public static Vector2 GetLegacyAutoOffset()
        {
            return LayoutSettingsSnapshot.Current.LegacyAutoOffset;
        }

        public static Vector2 GetUserOffset()
        {
            return LayoutSettingsSnapshot.Current.UserOffset;
        }

        public static bool IsManualPositioningEnabled()
        {
            return LayoutSettingsSnapshot.Current.ManualPositioningEnabled;
        }

        public static bool ShouldReserveOriginalWidth()
        {
            return LayoutSettingsSnapshot.Current.ReserveOriginalWidth;
        }

        public static Vector2 GetSlotOffset(int slotIndex)
        {
            return LayoutSettingsSnapshot.Current.GetSlotOffset(slotIndex);
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
            LayoutSettingsSnapshot.Invalidate();
        }

        public static void ClearSlotOffsets()
        {
            ModDataStore.Modify<ModSettings>(ModDataStore.SettingsKey, settings => settings.SlotOffsets.Clear());
            ModDataStore.Save(ModDataStore.SettingsKey);
            LayoutSettingsSnapshot.Invalidate();
        }

        public static float GetContentWidth(int count)
        {
            return LayoutSettingsSnapshot.Current.GetContentWidth(count);
        }

        public static Vector2 ResolveAutoPosition(Rect2 anchorRect, Vector2 contentSize,
            Rect2 avoidRect, Rect2 viewportRect)
        {
            var preferredYOffset = LayoutSettingsSnapshot.Current.LegacyAutoOffset.Y;

            var c0 = new Vector2(anchorRect.End.X + AutoGap, anchorRect.Position.Y + preferredYOffset);
            var c1 = new Vector2(anchorRect.Position.X, anchorRect.End.Y + AutoGap);
            var c2 = new Vector2(anchorRect.Position.X, anchorRect.Position.Y - contentSize.Y - AutoGap);
            var c3 = new Vector2(anchorRect.Position.X - contentSize.X - AutoGap,
                anchorRect.Position.Y + preferredYOffset);
            var c4 = new Vector2(anchorRect.End.X + AutoGap,
                anchorRect.Position.Y + preferredYOffset + ExtraAvoidanceShift);

            var best = c0;
            var bestScore = ScoreCandidate(c0, contentSize, avoidRect, viewportRect);

            var score1 = ScoreCandidate(c1, contentSize, avoidRect, viewportRect);
            if (score1 < bestScore)
            {
                best = c1;
                bestScore = score1;
            }

            var score2 = ScoreCandidate(c2, contentSize, avoidRect, viewportRect);
            if (score2 < bestScore)
            {
                best = c2;
                bestScore = score2;
            }

            var score3 = ScoreCandidate(c3, contentSize, avoidRect, viewportRect);
            if (score3 < bestScore)
            {
                best = c3;
                bestScore = score3;
            }

            var score4 = ScoreCandidate(c4, contentSize, avoidRect, viewportRect);
            if (score4 < bestScore) best = c4;

            return best;
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

        private static float ScoreCandidate(Vector2 candidate, Vector2 contentSize, Rect2 avoidRect, Rect2 viewportRect)
        {
            var candidateRect = new Rect2(candidate, contentSize);
            var overlap = candidateRect.Intersection(avoidRect);
            var penalty = overlap.Size.X * overlap.Size.Y;

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
