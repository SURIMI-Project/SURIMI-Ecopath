namespace Ecopath.Models
{
    public class DispositionGrid
    {
        public required FleetSegment FleetSegment { get; set; }
        public required Species Species { get; set; } 
        public List<DispositionCell> DispositionCells { get; set; } = new List<DispositionCell>();
    }
}