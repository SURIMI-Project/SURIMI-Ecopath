using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using System.Xml.Linq;

namespace ControlledVocabularies.Analysis.Strategies
{
    /// <summary>
    /// Detects CODE/NAME field pairs using consistent matcher-based comparisons
    /// </summary>
    public class CodeNamePairStrategy : IFieldAnalysisStrategy
    {
        private readonly ExactFieldMatcher _exactMatcher = new();
        private readonly ContainsFieldMatcher _containsMatcher = new();

        public string Name => "CodeNamePair";
        public double Priority => 9.0; // Highest priority - most reliable

        public FieldAnalysisResult Analyze(string fieldName, List<string> sampleValues,
            IEnumerable<MultiLevelKey> allRecords, AnalysisContext context)
        {
            var result = new FieldAnalysisResult { StrategyName = Name };
            var normalizedFieldName = FieldPolicy.ForSchema(fieldName);

            // Get all normalized field names for consistent comparison
            var allNormalizedFields = allRecords.SelectMany(r => r.FieldNames)
                .Select(f => FieldPolicy.ForSchema(f))
                .Distinct()
                .ToHashSet();

            // Test for CODE field with corresponding NAME field
            if (ContainsTermUsingMatcher(normalizedFieldName, "code"))
            {
                var correspondingNameField = FindCorrespondingFieldUsingMatchers(
                    normalizedFieldName, allNormalizedFields, "code", "name");

                if (correspondingNameField != null)
                {
                    result.SuggestedKind = FieldKind.Code;
                    result.SuggestedStrategy = MatchStrategy.Exact;
                    result.SuggestedWeight = 8;
                    result.Confidence = 0.95;
                    result.Evidence.Add($"CODE field with corresponding NAME field detected");
                    result.Evidence.Add($"Corresponding field: {correspondingNameField}");
                    return result;
                }
            }

            // Test for NAME field with corresponding CODE field
            if (ContainsTermUsingMatcher(normalizedFieldName, "name") &&
                !ContainsTermUsingMatcher(normalizedFieldName, "code"))
            {
                var correspondingCodeField = FindCorrespondingFieldUsingMatchers(
                    normalizedFieldName, allNormalizedFields, "name", "code");

                if (correspondingCodeField != null)
                {
                    result.SuggestedKind = FieldKind.Label;
                    result.SuggestedStrategy = MatchStrategy.Exact | MatchStrategy.Fuzzy | MatchStrategy.TokenOverlap;
                    result.SuggestedWeight = 6;
                    result.Confidence = 0.9;
                    result.Evidence.Add($"NAME field with corresponding CODE field detected");
                    result.Evidence.Add($"Corresponding field: {correspondingCodeField}");
                }
            }

            return result;
        }

        private bool ContainsTermUsingMatcher(string fieldName, string term)
        {
            var normalizedTerm = FieldPolicy.ForSchema(term);
            return _containsMatcher.Score(normalizedTerm, fieldName) > 0;
        }

        private string? FindCorrespondingFieldUsingMatchers(string fieldName, HashSet<string> allFields,
            string suffix1, string suffix2)
        {
            var normalizedSuffix1 = FieldPolicy.ForSchema(suffix1);
            var normalizedSuffix2 = FieldPolicy.ForSchema(suffix2);

            // Check if field ends with suffix1
            if (!fieldName.EndsWith(normalizedSuffix1)) return null;

            // Extract base name and construct target name
            var baseName = fieldName.Substring(0, fieldName.Length - normalizedSuffix1.Length);
            var targetName = baseName + normalizedSuffix2;

            // Use exact matcher for correspondence detection
            return allFields.FirstOrDefault(f => _exactMatcher.Score(targetName, f) > 0);
        }
    }

    /// <summary>
    /// Detects hierarchical structure patterns like "DL.FR[IS]TR" or "9.32"
    /// </summary>
    public class HierarchicalStructureStrategy : IFieldAnalysisStrategy
    {
        public string Name => "HierarchicalStructure";
        public double Priority => 8.0;

        public FieldAnalysisResult Analyze(string fieldName, List<string> sampleValues,
            IEnumerable<MultiLevelKey> allRecords, AnalysisContext context)
        {
            var result = new FieldAnalysisResult { StrategyName = Name };

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

        private HierarchicalAnalysis AnalyzeHierarchicalPatterns(List<string> values)
        {
            var analysis = new HierarchicalAnalysis();
            var commonSeparators = new[] { '.', '-', '_', ':', '[', ']', '(', ')' };
            var separatorCounts = new Dictionary<char, int>();
            var patternCounts = new Dictionary<string, int>();

            foreach (var value in values.Take(50)) // Sample for performance
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
            var totalValues = values.Count;
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

    /// <summary>
    /// Detects repetitive but meaningful patterns (like gear codes that repeat across records)
    /// </summary>
    public class RepetitiveMeaningfulStrategy : IFieldAnalysisStrategy
    {
        private readonly ContainsFieldMatcher _containsMatcher = new();

        public string Name => "RepetitiveMeaningful";
        public double Priority => 7.0;

        public FieldAnalysisResult Analyze(string fieldName, List<string> sampleValues,
            IEnumerable<MultiLevelKey> allRecords, AnalysisContext context)
        {
            var result = new FieldAnalysisResult { StrategyName = Name };

            if (sampleValues.Count < 5) return result; // Need sufficient sample

            var normalizedFieldName = FieldPolicy.ForSchema(fieldName);
            var uniqueValues = sampleValues.Distinct().ToList();
            var uniquenessRatio = (double)uniqueValues.Count / sampleValues.Count;

            // Check for repetitive pattern (low uniqueness)
            var isRepetitive = uniquenessRatio < 0.3 && uniqueValues.Count > 1;

            if (isRepetitive)
            {
                // Check if field name suggests meaningful categorization
                var meaningfulTerms = new[] { "gear", "category", "type", "group", "class" };
                var isMeaningful = meaningfulTerms.Any(term =>
                    _containsMatcher.Score(FieldPolicy.ForSchema(term), normalizedFieldName) > 0);

                // Check if field name suggests it's a code
                var isCodeField = _containsMatcher.Score(FieldPolicy.ForSchema("code"), normalizedFieldName) > 0;

                if (isMeaningful && isCodeField)
                {
                    result.SuggestedKind = FieldKind.Code;
                    result.SuggestedStrategy = MatchStrategy.Exact | MatchStrategy.ForeignKey;
                    result.SuggestedWeight = 7;
                    result.Confidence = Math.Min(0.85, 0.5 + (1 - uniquenessRatio)); // Higher confidence for more repetition
                    result.Evidence.Add($"Repetitive meaningful pattern: {uniquenessRatio:P1} uniqueness ({uniqueValues.Count} unique values in {sampleValues.Count} records)");
                    result.Evidence.Add($"Field name suggests categorization: {string.Join(", ", meaningfulTerms.Where(term => _containsMatcher.Score(FieldPolicy.ForSchema(term), normalizedFieldName) > 0))}");
                    result.Evidence.Add("Likely classification code with foreign key potential");
                    result.Metadata["UniquenessRatio"] = uniquenessRatio;
                    result.Metadata["UniqueValueCount"] = uniqueValues.Count;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Fallback strategy using basic statistical analysis
    /// </summary>
    public class BasicStatisticsStrategy : IFieldAnalysisStrategy
    {
        public string Name => "BasicStatistics";
        public double Priority => 1.0; // Lowest priority - fallback only

        public FieldAnalysisResult Analyze(string fieldName, List<string> sampleValues,
            IEnumerable<MultiLevelKey> allRecords, AnalysisContext context)
        {
            var result = new FieldAnalysisResult { StrategyName = Name };

            if (!sampleValues.Any()) return result;

            var normalizedFieldName = FieldPolicy.ForSchema(fieldName);
            var avgLength = sampleValues.Average(v => v.Length);
            var uniqueness = sampleValues.Distinct().Count() / (double)sampleValues.Count;
            var uppercaseRatio = CalculateUppercaseRatio(sampleValues);
            var allNumeric = sampleValues.All(v => double.TryParse(v, out _));
            var hasUriPattern = sampleValues.Any(v => v.StartsWith("http") || v.Contains("://"));

            // Basic kind inference
            FieldKind suggestedKind;
            if (hasUriPattern)
                suggestedKind = FieldKind.Uri;
            else if (allNumeric)
                suggestedKind = FieldKind.Numeric;
            else if ((avgLength <= 6 || normalizedFieldName.Contains("code")) && uppercaseRatio >= 0.7 && uniqueness >= 0.7)
                suggestedKind = FieldKind.Code;
            else
                suggestedKind = FieldKind.Label;

            // Basic strategy inference
            var suggestedStrategy = suggestedKind switch
            {
                FieldKind.Code => MatchStrategy.Exact,
                FieldKind.Label => MatchStrategy.Exact | MatchStrategy.Fuzzy,
                FieldKind.Uri => MatchStrategy.Exact,
                FieldKind.Numeric => MatchStrategy.Exact,
                _ => MatchStrategy.Exact
            };

            result.SuggestedKind = suggestedKind;
            result.SuggestedStrategy = suggestedStrategy;
            result.SuggestedWeight = 3; // Default weight
            result.Confidence = 0.3; // Low confidence - this is fallback logic
            result.Evidence.Add($"Basic statistical analysis: avg length={avgLength:F1}, uniqueness={uniqueness:P1}, uppercase={uppercaseRatio:P1}");

            return result;
        }

        private double CalculateUppercaseRatio(List<string> values)
        {
            if (!values.Any()) return 0;

            var uppercaseCount = values.Count(v =>
                v.All(c => char.IsUpper(c) || char.IsDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)));

            return (double)uppercaseCount / values.Count;
        }
    }

    /// <summary>
    /// Domain-specific term detection using vocabulary matching
    /// </summary>
    public class DomainSpecificTermStrategy : IFieldAnalysisStrategy
    {
        private readonly IVocabularyRegistry? _registry;
        private readonly GenericVocabularyMatcher _vocabMatcher;

        public DomainSpecificTermStrategy(IVocabularyRegistry? registry = null)
        {
            _registry = registry;
            _vocabMatcher = new GenericVocabularyMatcher(_registry);
        }
        public string Name => "DomainSpecific";
        public double Priority => 1.0; // Lowest priority - fallback only

        public FieldAnalysisResult Analyze(string fieldName, List<string> sampleValues,
            IEnumerable<MultiLevelKey> allRecords, AnalysisContext context)
        {
            var result = new FieldAnalysisResult { StrategyName = Name };

            if (_registry == null) return result;

            // Test field name against domain-specific vocabularies using matchers
            var domainConfidence = TestFieldNameAgainstDomainVocabularies(fieldName);

            // Test sample values against vocabularies
            var contentConfidence = TestSampleValuesAgainstVocabularies(fieldName, sampleValues);

            if (domainConfidence > 0.5 || contentConfidence > 0.5)
            {
                result.Confidence = Math.Max(domainConfidence, contentConfidence);
                result.SuggestedKind = FieldKind.Code; // Domain terms usually indicate codes
                result.Evidence.Add($"Domain matching: field name confidence {domainConfidence:F2}, content confidence {contentConfidence:F2}");
            }

            return result;
        }

        private double TestFieldNameAgainstDomainVocabularies(string fieldName)
        {
            var normalizedFieldName = FieldPolicy.ForSchema(fieldName);
            double bestScore = 0;

            // Test against all vocabulary field names using your existing matcher infrastructure
            foreach (var vocab in _registry!.GetAll())
            {
                foreach (var vocabFieldName in vocab.FieldNames)
                {
                    var normalizedVocabField = FieldPolicy.ForSchema(vocabFieldName);

                    // Use fuzzy matcher for field name similarity
                    var score = new FuzzyFieldMatcher().Score(normalizedFieldName, normalizedVocabField);
                    bestScore = Math.Max(bestScore, score);
                }
            }

            return bestScore;
        }

        private double TestSampleValuesAgainstVocabularies(string fieldName, List<string> sampleValues)
        {
            if (!sampleValues.Any()) return 0;

            var testKey = MultiLevelKey.FromPairs([(fieldName, sampleValues.First())], KeyDomain.NotSet);
            double bestScore = 0;

            foreach (var vocab in _registry!.GetAll())
            {
                // Use your existing vocabulary matcher - ultimate API consistency!
                var result = _vocabMatcher.Match(testKey, vocab, vocab);
                bestScore = Math.Max(bestScore, result.Score / 100.0); // Normalize to 0-1
            }

            return bestScore;
        }
    }
}