namespace Ecopath.Models
{
    public class BiomassGrid
    {
        public required Species Species { get; set; }
        public List<BiomassCell> BiomassCells { get; set; } = new List<BiomassCell>();

        override public string ToString()
        {
            return $"{Species}, {BiomassCells.Count} cells";
        }
    }
}
