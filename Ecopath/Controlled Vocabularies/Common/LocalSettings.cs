using ControlledVocabularies.Inference.Field.Strategies;

namespace ControlledVocabularies.Common
{
    public static class LocalSettings
    {
        // Matching baseline
        public static int DefaultMinScore { get; set; } = 80;

        /// <summary>if &lt;= 0: use all records</summary>
        public static int DefaultMaxSamples { get; set; } = 200;

        /// <summary>Keep unique values only</summary>
        public static bool DefaultDeduplicate { get; set; } = true;

        // --- Heuristics for BasicStatisticsStrategy ---
        public static int Heuristics_MaxShortLabel { get; set; } = 20;
        public static int Heuristics_MaxMediumLabel { get; set; } = 35;
        public static int Heuristics_MaxLikelyCodeLen { get; set; } = 10;
        public static double Heuristics_MinUppercaseRatioForCode { get; set; } = 0.70;
        public static double Heuristics_MinUniquenessForCode { get; set; } = 0.70;
        public static bool Heuristics_EnableKeywordStrategy { get; set; } = true;
    }
}
