using System.Text.Json.Serialization;

namespace SURIMIdto
{
    public sealed class SemanticsDomainDto
    {
        [JsonPropertyName("Domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonPropertyName("Mappings")]
        public List<SemanticsMappingDto> Mappings { get; set; } = new();
    }
}