namespace Ecopath.Models
{
    public class BiomassGrid
    {
        public required Species Species { get; set; }
        public List<BiomassCell> BiomassCells { get; set; } = new List<BiomassCell>();
    }
}
