namespace Ecopath.Models
{
    public class SalesSummary
    {
        public required string MarketId { get; set; }
        public required string MeasurementUnit { get; set; }
        public required string Currency { get; set; }
        public List<Sale> Sales { get; set; } = new List<Sale>();
    }
}