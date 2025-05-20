namespace Ecopath.Models
{
    public class Biomass
    {
        public string? MeasurementUnit { get; set; }
        public List<BiomassGrid> BiomassGrids { get; set; } = new List<BiomassGrid>();
    }
}
