namespace Ecopath.Models
{
    public class CatchDispositionSummary
    {
        public required string MeasurementUnit { get; set; }
        public List<DispositionGrid> DispositionGrids { get; set; } = new List<DispositionGrid>();
    }
}