namespace Ecopath.Models
{
    public class BiomassGrid
    {
        public required string SpeciesCode { get; set; }
        public List<BiomassCell> BiomassCells { get; set; } = new List<BiomassCell>();
    }
}
