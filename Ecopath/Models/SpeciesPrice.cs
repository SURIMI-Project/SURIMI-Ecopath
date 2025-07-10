namespace Ecopath.Models
{
    public class SpeciesPrice
    {
        public required string SpeciesCode { get; set; }
        public required string MarketCode { get; set; }
        public double Price { get; set; }
        public required string MeasuremenyUnit { get; set; }
        public required string Currency { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
