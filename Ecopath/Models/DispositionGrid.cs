namespace Ecopath.Models
{
    public class DispositionGrid
    {
        public required string GearCode { get; set; } // 3-alpha code of https://www.fao.org/fishery/en/collection/geartype
        public required string SpeciesCode { get; set; } // 3-alpha code of https://www.fao.org/fishery/en/collection/asfis
        public List<DispositionCell> DispositionCells { get; set; } = new List<DispositionCell>();
    }
}