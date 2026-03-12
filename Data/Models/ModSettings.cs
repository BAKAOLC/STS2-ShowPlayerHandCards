using System.Text.Json.Serialization;
using STS2ShowPlayerHandCards.Utils;

namespace STS2ShowPlayerHandCards.Data.Models
{
    public class ModSettings
    {
        [JsonPropertyName("data_version")] public int DataVersion { get; set; } = 1;

        /// <summary>
        ///     The key used to toggle hand card visibility.
        ///     Stored as string for JSON serialization.
        /// </summary>
        [JsonPropertyName("toggle_key")]
        public string ToggleKey { get; set; } = InputHandler.DefaultToggleKey.ToString();
    }
}
