using System.Text.Json.Serialization;
using STS2ShowPlayerHandCards.Utils;

namespace STS2ShowPlayerHandCards.Data.Models
{
    public enum HighlightMatchMode
    {
        Text,
        Regex,
        Template,
    }

    public sealed class HighlightKeywordEntry
    {
        [JsonPropertyName("keyword")] public string Keyword { get; set; } = string.Empty;
    }

    public sealed class HighlightRuleEntry
    {
        [JsonPropertyName("pattern")] public string Pattern { get; set; } = string.Empty;
        [JsonPropertyName("color_hex")] public string ColorHex { get; set; } = string.Empty;
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("match_mode")] public HighlightMatchMode MatchMode { get; set; } = HighlightMatchMode.Text;
        [JsonPropertyName("keywords")] public List<string> Keywords { get; set; } = [];
        [JsonPropertyName("types")] public List<string> Types { get; set; } = [];
        [JsonPropertyName("rarities")] public List<string> Rarities { get; set; } = [];
        [JsonPropertyName("target_types")] public List<string> TargetTypes { get; set; } = [];
        [JsonPropertyName("effect_terms")] public List<string> EffectTerms { get; set; } = [];
        [JsonPropertyName("require_upgraded")] public bool? RequireUpgraded { get; set; }
        [JsonPropertyName("require_playable")] public bool? RequirePlayable { get; set; }
    }

    public sealed class SlotPositionEntry
    {
        [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
        [JsonPropertyName("offset_x")] public double OffsetX { get; set; }
        [JsonPropertyName("offset_y")] public double OffsetY { get; set; }
    }

    public class ModSettings
    {
        public const int CurrentDataVersion = 4;
        public const double MinContentScale = 0.5d;
        public const double MaxContentScale = 5.0d;
        public const double MinPositionOffset = -200d;
        public const double MaxPositionOffset = 200d;

        [JsonPropertyName("data_version")] public int DataVersion { get; set; } = CurrentDataVersion;

        [JsonPropertyName("toggle_key")] public string ToggleKey { get; set; } = InputHandler.DefaultToggleBinding;

        [JsonPropertyName("content_scale")] public double ContentScale { get; set; } = 1.0d;

        [JsonPropertyName("position_offset_x")]
        public double PositionOffsetX { get; set; }

        [JsonPropertyName("position_offset_y")]
        public double PositionOffsetY { get; set; }

        [JsonPropertyName("manual_positioning_enabled")]
        public bool ManualPositioningEnabled { get; set; }

        [JsonPropertyName("reserve_original_width")]
        public bool ReserveOriginalWidth { get; set; } = true;

        [JsonPropertyName("slot_offsets")] public List<SlotPositionEntry> SlotOffsets { get; set; } = [];

        [JsonPropertyName("highlight_rules")] public List<HighlightRuleEntry> HighlightRules { get; set; } = [];

        [JsonPropertyName("highlight_keywords")]
        public List<HighlightKeywordEntry> HighlightKeywords { get; set; } = [];

        [JsonPropertyName("highlight_color_hex")]
        public string HighlightColorHex { get; set; } = "#FFD740FF";
    }
}
