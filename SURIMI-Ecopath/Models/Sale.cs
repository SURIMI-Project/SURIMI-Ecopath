namespace Ecopath.Models
{
    public class Sale
    {
        public required string SpeciesCode { get; set; }
        public required string GearCode { get; set; }
        public double Quantity = 2;
        public double Value = 3;
    }
}