using ControlledVocabularies.Inference.Field.Strategies;

namespace ControlledVocabularies.Common
{
    public class LocalSettings
    {
        public static int DefaultMinScore { get; set; } = 80;

        /// <summary>
        /// if <=0 : use all records
        /// </summary>
        public static int DefaultMaxSamples { get; set; } = 200;

        /// <summary>
        /// Keep unique values only (recommended to bound work even when using all records)
        /// </summary>
        public static bool DefaultDeduplicate { get; set; } = true;
    }
}
