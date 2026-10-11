using System.Text.Json.Serialization;

namespace SURIMIdto
{
    public sealed class SemanticsFileDto
    {
        [JsonPropertyName("Domains")]
        public List<SemanticsDomainDto> Domains { get; set; } = new();
    }
}
