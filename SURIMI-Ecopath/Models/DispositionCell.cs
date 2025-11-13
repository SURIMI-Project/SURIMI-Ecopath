namespace Ecopath.Models
{
    public class DispositionCell
    {
        public double Longitude { get; set; }
        public double Latitude { get; set; }

        /// <summary>
        /// ToDo: Explicitly define if catches should include live and dead discards. EwE catches only includes dead discards; live discards are fed right back into the system
        /// </summary>
        public double GrossCatchBiomass { get; set; }
        public double LiveDiscardsBiomass { get; set; }
        public double DeadDiscardsBiomass { get; set; }
    }
}