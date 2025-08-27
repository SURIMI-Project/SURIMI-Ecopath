using ControlledVocabularies.Match;

namespace ControlledVocabularies.Inference
{
    public class ForeignKeyMatchResult : MatchResult
    {
        public int MatchCount { get; set; }
        public double Confidence { get; set; }

        // Score inherited from MatchResult (0-100 integer)
        // Justification inherited from MatchResult 
        // All other MatchResult fields inherited (SourceField, TargetField, etc.)
    }
}