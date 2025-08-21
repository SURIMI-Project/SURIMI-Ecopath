using ControlledVocabularies.Core;

namespace ControlledVocabularies.Match
{
    /// <summary>
    /// Base class for any match result involving a <see cref="MultiLevelKey"/> record.
    /// </summary>
    public class MatchResult
    {
        public string SourceField { get; set; } = string.Empty;
        public string SourceFieldValue { get; set; } = string.Empty;
        public string SourceVocabulary { get; set; } = string.Empty;

        public string TargetField { get; set; } = string.Empty;
        public string TargetFieldValue { get; set; } = string.Empty;
        public string TargetVocabulary { get; set; } = string.Empty;

        /// <summary>
        /// The record that was found as a match.
        /// </summary>
        public MultiLevelKey MatchedKey { get; set; } = MultiLevelKey.Empty;

        /// <summary>
        /// The similarity score, scaled from 0 to 100.
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// Get the match strategy used.
        /// </summary>
        public MatchStrategy StrategyUsed { get; set; }

        /// <summary>
        /// A short description or reason for why/how this match was made.
        /// </summary>
        public string Justification { get; set; } = string.Empty;

        /// <summary>
        /// Get whether this result qualifies as a match at all.
        /// </summary>
        public virtual bool IsMatch => Score > 0;

        public override string ToString()
        {
            return $"{SourceField}:{SourceFieldValue} → {TargetField}:{TargetFieldValue} [{Score}] {StrategyUsed} ({Justification})";
        }

        public static MatchResult NoMatch => new MatchResult { Score = 0, Justification = "No match" };
    }
}