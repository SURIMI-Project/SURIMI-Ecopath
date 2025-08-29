using ControlledVocabularies.Core;

namespace ControlledVocabularies.Inference.Field
{
    /// <summary>
    /// Analysis result from a single strategy
    /// </summary>
    public class FieldInferenceResult
    {
        public string StrategyName { get; set; } = "";
        public double Confidence { get; set; } // 0.0 - 1.0
        public FieldKind? SuggestedKind { get; set; }
        public MatchStrategy? SuggestedStrategy { get; set; }
        public int? SuggestedWeight { get; set; }
        public List<string> Evidence { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();

        public bool HasSuggestion => SuggestedKind.HasValue || SuggestedStrategy.HasValue || SuggestedWeight.HasValue;

        public override string ToString()
        {
            return $"Analysis: {StrategyName} {Confidence} => {SuggestedKind}, {SuggestedStrategy} @ {SuggestedWeight}";
        }
    }
}