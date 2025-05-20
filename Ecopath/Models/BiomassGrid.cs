namespace Ecopath.Models
{
    public class BiomassGrid
    {
        public string SpeciesId { get; set; } = string.Empty;
        public List<BiomassCell> BiomassCells { get; set; } = new List<BiomassCell>();
    }
}
