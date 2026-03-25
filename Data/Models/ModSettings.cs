using System.Text.Json.Serialization;
using STS2ShowPlayerHandCards.Utils;

namespace STS2ShowPlayerHandCards.Data.Models
{
    public sealed class HighlightKeywordEntry
    {
        [JsonPropertyName("keyword")] public string Keyword { get; set; } = string.Empty;
    }

    public class ModSettings
    {
        public const int CurrentDataVersion = 1;

        [JsonPropertyName("data_version")] public int DataVersion { get; set; } = CurrentDataVersion;

        /// <summary>
        ///     The key used to toggle hand card visibility.
        ///     Stored as string for JSON serialization.
        /// </summary>
        [JsonPropertyName("toggle_key")]
        public string ToggleKey { get; set; } = InputHandler.DefaultToggleBinding;

        [JsonPropertyName("content_scale")] public float ContentScale { get; set; } = 1.0f;

        [JsonPropertyName("highlight_keywords")]
        public List<HighlightKeywordEntry> HighlightKeywords { get; set; } = [];

        [JsonPropertyName("highlight_color_hex")]
        public string HighlightColorHex { get; set; } = "#FFD740FF";
    }
}
