using System.Text.Json.Serialization;

namespace SURIMIdto
{
    public sealed class SemanticsMappingDto
    {
        [JsonPropertyName("Source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("Target")]
        public string Target { get; set; } = string.Empty;
    }
}
