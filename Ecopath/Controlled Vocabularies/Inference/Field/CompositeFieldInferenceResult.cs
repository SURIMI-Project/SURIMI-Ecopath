using ControlledVocabularies.Core;

namespace ControlledVocabularies.Inference.Field
{
    public class CompositeFieldInferenceResult
    {
        public string FieldName { get; }
        public List<FieldInferenceResult> StrategyResults { get; }

        public FieldKind RecommendedKind { get; }
        public MatchStrategy RecommendedStrategy { get; }
        public int RecommendedWeight { get; }
        public int OverallConfidence { get; }
        public string ConsensusMethod { get; }

        public CompositeFieldInferenceResult(string fieldName, List<FieldInferenceResult> results)
        {
            FieldName = fieldName;
            StrategyResults = new List<FieldInferenceResult>(results);

            var consensus = CalculateHybridConsensus(results);
            RecommendedKind = consensus.Kind;
            RecommendedStrategy = consensus.Strategy;
            RecommendedWeight = consensus.Weight;
            OverallConfidence = consensus.Confidence;
            ConsensusMethod = consensus.Method;
        }

        private ConsensusResult CalculateHybridConsensus(List<FieldInferenceResult> results)
        {
            if (results.Count == 0)
                return ConsensusResult.NoResults();

            var validResults = new List<FieldInferenceResult>();
            foreach (var result in results)
            {
                if (result.HasSuggestion)
                    validResults.Add(result);
            }

            if (validResults.Count == 0)
                return ConsensusResult.NoSuggestions();


            var highConfidenceResults = new List<FieldInferenceResult>();
            foreach (var result in results)
            {
                if (result.ConfidenceScore >= 80 && result.SuggestedKind.HasValue)
                    highConfidenceResults.Add(result);
            }

            if (highConfidenceResults.Count > 0)
                return CalculateHighConfidenceConsensus(highConfidenceResults);

            return CalculateVoteTallyConsensus(results);
        }

        private ConsensusResult CalculateHighConfidenceConsensus(List<FieldInferenceResult> highConfidenceResults)
        {
            FieldInferenceResult winner = highConfidenceResults[0];
            foreach (var result in highConfidenceResults)
            {
                if (result.ConfidenceScore > winner.ConfidenceScore)
                    winner = result;
            }

            var strategy = winner.SuggestedStrategy ?? CalculateAverageStrategy(highConfidenceResults);
            var weight = winner.SuggestedWeight ?? CalculateAverageWeight(highConfidenceResults);

            int totalConfidence = 0;
            foreach (var result in highConfidenceResults)
            {
                totalConfidence += result.ConfidenceScore;
            }
            var avgConfidence = totalConfidence / highConfidenceResults.Count;

            return new ConsensusResult
            {
                Kind = winner.SuggestedKind!.Value,
                Strategy = strategy,
                Weight = weight,
                Confidence = avgConfidence,
                Method = "HighConfidenceOverride"
            };
        }

        private ConsensusResult CalculateVoteTallyConsensus(List<FieldInferenceResult> normalizedResults)
        {
            var kindGroups = new Dictionary<FieldKind, List<FieldInferenceResult>>();

            foreach (var result in normalizedResults)
            {
                if (result.SuggestedKind.HasValue)
                {
                    var kind = result.SuggestedKind.Value;
                    if (!kindGroups.ContainsKey(kind))
                        kindGroups[kind] = new List<FieldInferenceResult>();

                    kindGroups[kind].Add(result);
                }
            }

            if (kindGroups.Count == 0)
                return ConsensusResult.NoKindVotes();

            var kindVotes = new List<KindVote>();
            foreach (var group in kindGroups)
            {
                int totalScore = 0;
                int totalConfidence = 0;
                var contributingResults = new List<FieldInferenceResult>();

                foreach (var result in group.Value)
                {
                    totalScore += result.ConfidenceScore;
                    totalConfidence += result.ConfidenceScore;
                    contributingResults.Add(result);
                }

                var vote = new KindVote
                {
                    Kind = group.Key,
                    TotalScore = totalScore,
                    AverageConfidence = totalConfidence / group.Value.Count,
                    ContributingResults = contributingResults
                };
                kindVotes.Add(vote);
            }

            KindVote winningVote = kindVotes[0];
            foreach (var vote in kindVotes)
            {
                if (vote.TotalScore > winningVote.TotalScore)
                    winningVote = vote;
                else if (vote.TotalScore == winningVote.TotalScore && vote.AverageConfidence > winningVote.AverageConfidence)
                    winningVote = vote;
            }

            var strategy = CalculateAverageStrategy(winningVote.ContributingResults);
            var weight = CalculateAverageWeight(winningVote.ContributingResults);

            return new ConsensusResult
            {
                Kind = winningVote.Kind,
                Strategy = strategy,
                Weight = weight,
                Confidence = winningVote.AverageConfidence,
                Method = "VoteTally"
            };
        }

        private int CalculateAverageWeight(List<FieldInferenceResult> results)
        {
            var resultsWithWeights = new List<WeightedWeight>();
            foreach (var result in results)
            {
                if (result.SuggestedWeight.HasValue)
                {
                    var weightedWeight = new WeightedWeight
                    {
                        Weight = result.SuggestedWeight.Value,
                        Confidence = result.Confidence
                    };
                    resultsWithWeights.Add(weightedWeight);
                }
            }

            return CalculateAverageWeightFromWeightedWeights(resultsWithWeights);
        }

        private int CalculateAverageWeightFromWeightedWeights(List<WeightedWeight> weightedWeights)
        {
            if (weightedWeights.Count == 0) return 1;

            double weightedSum = 0;
            double confidenceSum = 0;

            foreach (var w in weightedWeights)
            {
                weightedSum += w.Weight * w.Confidence;
                confidenceSum += w.Confidence;
            }

            var result = (int)Math.Round(weightedSum / confidenceSum);
            if (result < 1) return 1;
            if (result > 10) return 10;
            return result;
        }

        private MatchStrategy CalculateAverageStrategy(List<FieldInferenceResult> normalizedResults)
        {
            MatchStrategy total = MatchStrategy.None;
            foreach (var result in normalizedResults)
            {
                if (result.SuggestedStrategy.HasValue)
                {
                    total |= result.SuggestedStrategy!.Value;
                }
            }
            return total;
        }
        public bool MeetsThreshold(int minScore = 50) => OverallConfidence >= minScore;

        public IEnumerable<string> GetAllEvidence()
        {
            var allEvidence = new List<string>();
            foreach (var result in StrategyResults)
            {
                foreach (var evidence in result.Evidence)
                {
                    allEvidence.Add($"[{result.StrategyName}] {evidence}");
                }
            }
            return allEvidence;
        }

        public IEnumerable<string> GetAllWarnings()
        {
            var allWarnings = new List<string>();
            foreach (var result in StrategyResults)
            {
                foreach (var warning in result.Warnings)
                {
                    allWarnings.Add($"[{result.StrategyName}] {warning}");
                }
            }
            return allWarnings;
        }

        public override string ToString()
        {
            var evidence = StrategyResults.Count > 0 ? $" ({StrategyResults.Count} strategies)" : "";
            return $"Composite[{FieldName}]: {RecommendedKind}, {RecommendedStrategy} @ {RecommendedWeight} " +
                   $"[{OverallConfidence}/100] via {ConsensusMethod}{evidence}";
        }
    }

    public class KindVote
    {
        public FieldKind Kind { get; set; }
        public int TotalScore { get; set; }
        public int AverageConfidence { get; set; }
        public List<FieldInferenceResult> ContributingResults { get; set; } = new();

        public override string ToString() => $"{Kind}: {TotalScore} points ({ContributingResults.Count} votes)";
    }

    public class ConsensusResult
    {
        public FieldKind Kind { get; set; }
        public MatchStrategy Strategy { get; set; }
        public int Weight { get; set; }
        public int Confidence { get; set; }
        public string Method { get; set; } = "";

        public static ConsensusResult NoResults() => new ConsensusResult
        {
            Kind = FieldKind.Unknown,
            Strategy = MatchStrategy.None,
            Weight = 1,
            Confidence = 0,
            Method = "NoResults"
        };

        public static ConsensusResult NoSuggestions() => new ConsensusResult
        {
            Kind = FieldKind.Unknown,
            Strategy = MatchStrategy.None,
            Weight = 1,
            Confidence = 0,
            Method = "NoSuggestions"
        };

        public static ConsensusResult NoKindVotes() => new ConsensusResult
        {
            Kind = FieldKind.Unknown,
            Strategy = MatchStrategy.None,
            Weight = 1,
            Confidence = 0,
            Method = "NoKindVotes"
        };

        public override string ToString() => $"{Kind} via {Method} [{Confidence}/100]";
    }

    public class WeightedWeight
    {
        public int Weight { get; set; }
        public double Confidence { get; set; }

        public override string ToString() => $"Weight {Weight} @ {Confidence:F2} confidence";
    }
}