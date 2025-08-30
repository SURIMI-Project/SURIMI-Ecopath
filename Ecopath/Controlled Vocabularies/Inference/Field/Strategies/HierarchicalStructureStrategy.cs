using ControlledVocabularies.Context;
using ControlledVocabularies.Core;

namespace ControlledVocabularies.Inference.Field.Strategies
{
    /// <summary>
    /// Detects hierarchical structure patterns like "DL.FR[IS]TR" or "9.32"
    /// </summary>
    public class HierarchicalStructureStrategy : IFieldInferenceStrategy
    {
        public string Name => "HierarchicalStructure";
        public double Priority => 8.0;

        public FieldInferenceResult Analyze(string fieldName, IEnumerable<string> sampleValues, IEnumerable<MultiLevelKey> allRecords, ModelContext context)
        {
            var result = new FieldInferenceResult { StrategyName = Name };

            if (!sampleValues.Any()) return result;

            var hierarchicalAnalysis = AnalyzeHierarchicalPatterns(sampleValues);

            if (hierarchicalAnalysis.IsHierarchical && hierarchicalAnalysis.ConsistencyRatio > 0.5)
            {
                result.SuggestedKind = FieldKind.Code;
                result.SuggestedStrategy = MatchStrategy.Exact;
                result.SuggestedWeight = 7;
                result.Confidence = hierarchicalAnalysis.ConsistencyRatio;
                result.Evidence.Add($"Hierarchical pattern detected with {hierarchicalAnalysis.ConsistencyRatio:P1} consistency");
                result.Evidence.Add($"Structure depth: {hierarchicalAnalysis.Depth}, separators: [{string.Join(", ", hierarchicalAnalysis.Separators)}]");
                result.Metadata["HierarchicalDepth"] = hierarchicalAnalysis.Depth;
                result.Metadata["Separators"] = hierarchicalAnalysis.Separators;
                result.Metadata["ConsistencyRatio"] = hierarchicalAnalysis.ConsistencyRatio;

                // Boost confidence for very consistent hierarchical patterns
                if (hierarchicalAnalysis.ConsistencyRatio > 0.8)
                {
                    result.Confidence = Math.Min(0.95, result.Confidence + 0.2);
                    result.Evidence.Add("High consistency hierarchical pattern - strong code indicator");
                }
            }

            return result;
        }

        private HierarchicalAnalysis AnalyzeHierarchicalPatterns(IEnumerable<string> values)
        {
            var analysis = new HierarchicalAnalysis();
            var commonSeparators = new[] { '.', '-', '_', ':', '[', ']', '(', ')' };
            var separatorCounts = new Dictionary<char, int>();
            var patternCounts = new Dictionary<string, int>();

            foreach (var value in values) // Sample for performance
            {
                if (string.IsNullOrWhiteSpace(value)) continue;

                // Count separators
                foreach (var sep in commonSeparators)
                {
                    if (value.Contains(sep))
                    {
                        separatorCounts[sep] = separatorCounts.GetValueOrDefault(sep) + 1;
                        var depth = value.Count(c => c == sep) + 1;
                        analysis.MaxDepth = Math.Max(analysis.MaxDepth, depth);
                    }
                }

                // Analyze structural patterns
                var pattern = ExtractStructuralPattern(value);
                if (!string.IsNullOrEmpty(pattern))
                {
                    patternCounts[pattern] = patternCounts.GetValueOrDefault(pattern) + 1;
                }
            }

            // Determine if hierarchical
            var totalValues = values.Count();
            var threshold = totalValues * 0.3; // 30% threshold for pattern recognition

            analysis.Separators = separatorCounts.Where(kv => kv.Value > threshold)
                .Select(kv => kv.Key).ToList();

            analysis.IsHierarchical = analysis.Separators.Any() && analysis.MaxDepth > 1;

            if (analysis.IsHierarchical && patternCounts.Any())
            {
                var mostCommonPattern = patternCounts.OrderByDescending(kv => kv.Value).First();
                analysis.ConsistencyRatio = (double)mostCommonPattern.Value / totalValues;
                analysis.Depth = analysis.MaxDepth;
            }

            return analysis;
        }

        private string ExtractStructuralPattern(string value)
        {
            // Convert "DL.FR[IS]TR" to pattern like "AA.AA[AA]AA"
            // Convert "9.32" to pattern like "N.NN"  
            var pattern = value;
            pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"[A-Z]", "A");
            pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"[a-z]", "a");
            pattern = System.Text.RegularExpressions.Regex.Replace(pattern, @"[0-9]", "N");
            return pattern;
        }

        private class HierarchicalAnalysis
        {
            public bool IsHierarchical { get; set; }
            public int MaxDepth { get; set; }
            public int Depth { get; set; }
            public List<char> Separators { get; set; } = new();
            public double ConsistencyRatio { get; set; }
        }
    }
}