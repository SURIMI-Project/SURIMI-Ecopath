namespace Ecopath.Models
{
    public class Species
    {
        public required string SpeciesCode { get; set; }
        public string Length {get; set; } = string.Empty;
        public string Age {get; set; } = string.Empty;
        public string Stage {get; set; } = string.Empty;

     }
}