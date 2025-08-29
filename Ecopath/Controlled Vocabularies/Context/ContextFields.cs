using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Context
{
    /// <summary>
    /// Canonical, schema-normalized field names for context.
    /// Always reference these constants (no magic strings).
    /// </summary>
    public static class ContextFields
    {
        // Identity (Metadata domain)
        public static readonly string ModelName = FieldPolicy.ForSchema("model_name");
        public static readonly string AreaName = FieldPolicy.ForSchema("area_name");

        // Geographic (Geographic domain / SpatialExtent purpose)
        public static readonly string MinLat = FieldPolicy.ForSchema("min_lat");
        public static readonly string MinLon = FieldPolicy.ForSchema("min_lon");
        public static readonly string MaxLat = FieldPolicy.ForSchema("max_lat");
        public static readonly string MaxLon = FieldPolicy.ForSchema("max_lon");

        // Temporal (Temporal domain / TimeStamp purpose)
        public static readonly string StartYear = FieldPolicy.ForSchema("start_year");
        public static readonly string EndYear = FieldPolicy.ForSchema("end_year");
    }
}
