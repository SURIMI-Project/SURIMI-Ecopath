using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference
{
    /// <summary>
    /// Infers semantic metadata for blank vocabularies by analyzing content patterns
    /// and testing foreign key relationships against known vocabularies.
    /// </summary>
    public class VocabularySemanticInferrer
    {
        private readonly IVocabularyRegistry _registry;
        private readonly KeyFieldIndexer _fieldIndexer;

        public VocabularySemanticInferrer(IVocabularyRegistry registry)
        {
            _registry = registry;
            _fieldIndexer = new KeyFieldIndexer();
        }

        public SemanticInferenceResult AnalyzeVocabulary(IControlledVocabulary blankVocab)
        {
            var result = new SemanticInferenceResult(blankVocab.VocabularyName);

            // 1. Analyze vocabulary name for domain hints
            InferVocabularyDomainFromName(blankVocab.VocabularyName, result);

            // 2. Analyze each field for semantic clues
            foreach (var fieldName in blankVocab.FieldNames)
            {
                AnalyzeField(blankVocab, fieldName, result);
            }

            // 3. Infer primary domain/purpose from field analysis
            InferPrimarySemantics(result);

            // 4. Test foreign key hypotheses against known vocabularies
            TestForeignKeyHypotheses(blankVocab, result);

            return result;
        }

        private void InferVocabularyDomainFromName(string vocabName, SemanticInferenceResult result)
        {
            var name = vocabName.ToLowerInvariant();

            if (name.Contains("species") || name.Contains("fish") || name.Contains("marine"))
                result.AddDomainHint(KeyDomain.Species, 0.7, "Vocabulary name suggests species");

            if (name.Contains("gear") || name.Contains("fishing") || name.Contains("fleet"))
                result.AddDomainHint(KeyDomain.FleetSegment, 0.8, "Vocabulary name suggests fishing");

            if (name.Contains("country") || name.Contains("nation") || name.Contains("iso"))
                result.AddDomainHint(KeyDomain.Country, 0.9, "Vocabulary name suggests geography");
        }

        private void AnalyzeField(IControlledVocabulary vocab, string fieldName, SemanticInferenceResult result)
        {
            var fieldInfo = new FieldInferenceInfo(fieldName);

            // Get sample values for pattern analysis
            var sampleValues = vocab.Records.Take(100)
                .Select(r => r.GetField(fieldName)?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Take(20)
                .ToList();

            if (!sampleValues.Any()) return;

            // Create temporary descriptor for field indexing
            var tempDescriptor = new KeyFieldDescriptor(fieldName, KeyDomain.NotSet, KeyPurpose.NotSet);
            _fieldIndexer.BuildIndex(fieldName, vocab.Records, tempDescriptor);

            // Analyze field name patterns
            InferFromFieldName(fieldName, fieldInfo);

            // Analyze content patterns  
            InferFromContent(sampleValues, tempDescriptor, fieldInfo);

            result.AddFieldInference(fieldInfo);
        }

        private void InferFromFieldName(string fieldName, FieldInferenceInfo fieldInfo)
        {
            var name = fieldName.ToLowerInvariant();

            // Species-related patterns
            if (name.Contains("species") || name.Contains("scientific") || name.Contains("binomial"))
            {
                fieldInfo.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.9, "Field name suggests species");
            }
            else if (name.Contains("common") && name.Contains("name"))
            {
                fieldInfo.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.7, "Common name field");
            }
            else if (name.Contains("stage") || name.Contains("life"))
            {
                fieldInfo.AddSemanticHint(KeyDomain.Species, KeyPurpose.Lifestage, 0.8, "Life stage field");
            }

            // Fishing-related patterns
            else if (name.Contains("gear"))
            {
                fieldInfo.AddSemanticHint(KeyDomain.FleetSegment, KeyPurpose.Gear, 0.9, "Gear field");
            }
            else if (name.Contains("fleet") || name.Contains("vessel"))
            {
                fieldInfo.AddSemanticHint(KeyDomain.FleetSegment, KeyPurpose.Fleet, 0.8, "Fleet field");
            }

            // Geographic patterns
            else if (name.Contains("country") || name.Contains("flag") || name.Contains("nation"))
            {
                fieldInfo.AddSemanticHint(KeyDomain.Country, KeyPurpose.Country, 0.9, "Country field");
            }

            // Code patterns (potential FKs)
            if (name.Contains("code") || name.Contains("id") || name == "fao" || name == "asfis")
            {
                fieldInfo.IsPotentialForeignKey = true;
                fieldInfo.ForeignKeyConfidence = name.Length <= 10 ? 0.8 : 0.5; // Shorter = more likely FK
            }
        }

        private void InferFromContent(List<string> samples, KeyFieldDescriptor descriptor, FieldInferenceInfo fieldInfo)
        {
            // Use existing field indexer insights
            if (descriptor.Kind == FieldKind.Code)
            {
                fieldInfo.IsPotentialForeignKey = true;
                fieldInfo.ForeignKeyConfidence = Math.Max(fieldInfo.ForeignKeyConfidence, 0.6);
            }

            // Analyze content patterns for semantic clues
            var avgLength = samples.Average(s => s.Length);
            var hasBinomials = samples.Any(s => s.Contains(' ') && s.Split(' ').Length == 2 && char.IsLower(s.Split(' ')[1][0]));
            var hasNumbers = samples.Any(s => s.Any(char.IsDigit));

            if (hasBinomials)
            {
                fieldInfo.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.95, "Contains binomial patterns");
            }

            if (avgLength <= 5 && hasNumbers && descriptor.UniquenessRatio > 0.8)
            {
                fieldInfo.IsPotentialForeignKey = true;
                fieldInfo.ForeignKeyConfidence = 0.9;
                fieldInfo.AddReason("Short, unique, alphanumeric codes suggest FK");
            }
        }

        private void InferPrimarySemantics(SemanticInferenceResult result)
        {
            // Find most confident domain/purpose from field analysis
            var domainVotes = new Dictionary<KeyDomain, double>();
            var purposeVotes = new Dictionary<KeyPurpose, double>();

            foreach (var field in result.FieldInferences)
            {
                foreach (var hint in field.SemanticHints)
                {
                    domainVotes[hint.Domain] = domainVotes.GetValueOrDefault(hint.Domain) + hint.Confidence;
                    purposeVotes[hint.Purpose] = purposeVotes.GetValueOrDefault(hint.Purpose) + hint.Confidence;
                }
            }

            if (domainVotes.Any())
            {
                var topDomain = domainVotes.OrderByDescending(kv => kv.Value).First();
                result.InferredDomain = topDomain.Key;
                result.DomainConfidence = Math.Min(1.0, topDomain.Value / domainVotes.Count);
            }

            if (purposeVotes.Any())
            {
                // Combine multiple purposes with flags
                var sortedPurposes = purposeVotes.OrderByDescending(kv => kv.Value).ToList();
                result.InferredPurpose = KeyPurpose.NotSet;

                foreach (var purpose in sortedPurposes.Where(kv => kv.Value > 1.0)) // Threshold for inclusion
                {
                    result.InferredPurpose |= purpose.Key;
                }

                if (result.InferredPurpose == KeyPurpose.NotSet && sortedPurposes.Any())
                    result.InferredPurpose = sortedPurposes.First().Key;
            }
        }

        private void TestForeignKeyHypotheses(IControlledVocabulary blankVocab, SemanticInferenceResult result)
        {
            var potentialFKFields = result.FieldInferences.Where(f => f.IsPotentialForeignKey).ToList();

            foreach (var fkField in potentialFKFields)
            {
                TestFieldAsForeignKey(blankVocab, fkField, result);
            }
        }

        private void TestFieldAsForeignKey(IControlledVocabulary sourceVocab, FieldInferenceInfo fkField, SemanticInferenceResult result)
        {
            // Test against all known vocabularies
            foreach (var targetVocab in _registry.GetAll())
            {
                if (ReferenceEquals(sourceVocab, targetVocab)) continue;

                var fkTest = TestForeignKeyMatch(sourceVocab, fkField.FieldName, targetVocab);
                if (fkTest.IsViable)
                {
                    result.AddForeignKeyCandidate(new ForeignKeyCandidate
                    {
                        SourceField = fkField.FieldName,
                        TargetVocabulary = targetVocab.VocabularyName,
                        TargetField = fkTest.BestTargetField,
                        MatchCount = fkTest.MatchCount,
                        MatchRatio = fkTest.MatchRatio,
                        Confidence = fkTest.MatchRatio * fkField.ForeignKeyConfidence
                    });
                }
            }
        }

        private ForeignKeyTestResult TestForeignKeyMatch(IControlledVocabulary source, string sourceField, IControlledVocabulary target)
        {
            var result = new ForeignKeyTestResult();
            var sourceValues = source.Records.Select(r => r.GetField(sourceField)?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet();

            if (!sourceValues.Any()) return result;

            // Test against each target field
            foreach (var targetField in target.FieldNames)
            {
                var targetValues = target.Records.Select(r => r.GetField(targetField)?.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToHashSet();

                if (!targetValues.Any()) continue;

                var matches = sourceValues.Intersect(targetValues).Count();
                var ratio = (double)matches / sourceValues.Count;

                if (ratio > result.MatchRatio)
                {
                    result.MatchCount = matches;
                    result.MatchRatio = ratio;
                    result.BestTargetField = targetField;
                }
            }

            return result;
        }
    }

    // Supporting classes for clean results
    public class SemanticInferenceResult
    {
        public string VocabularyName { get; }
        public KeyDomain InferredDomain { get; set; } = KeyDomain.NotSet;
        public KeyPurpose InferredPurpose { get; set; } = KeyPurpose.NotSet;
        public double DomainConfidence { get; set; }
        public List<FieldInferenceInfo> FieldInferences { get; } = new();
        public List<ForeignKeyCandidate> ForeignKeyCandidates { get; } = new();

        public SemanticInferenceResult(string vocabName) => VocabularyName = vocabName;

        public void AddDomainHint(KeyDomain domain, double confidence, string reason) { /* implementation */ }
        public void AddFieldInference(FieldInferenceInfo field) => FieldInferences.Add(field);
        public void AddForeignKeyCandidate(ForeignKeyCandidate fk) => ForeignKeyCandidates.Add(fk);
    }

    public class FieldInferenceInfo
    {
        public string FieldName { get; }
        public List<SemanticHint> SemanticHints { get; } = new();
        public bool IsPotentialForeignKey { get; set; }
        public double ForeignKeyConfidence { get; set; }
        public List<string> Reasons { get; } = new();

        public FieldInferenceInfo(string fieldName) => FieldName = fieldName;

        public void AddSemanticHint(KeyDomain domain, KeyPurpose purpose, double confidence, string reason)
        {
            SemanticHints.Add(new SemanticHint(domain, purpose, confidence, reason));
        }

        public void AddReason(string reason) => Reasons.Add(reason);
    }

    public record SemanticHint(KeyDomain Domain, KeyPurpose Purpose, double Confidence, string Reason);
    public record ForeignKeyCandidate
    {
        public string SourceField { get; init; } = "";
        public string TargetVocabulary { get; init; } = "";
        public string TargetField { get; init; } = "";
        public int MatchCount { get; init; }
        public double MatchRatio { get; init; }
        public double Confidence { get; init; }
    }

    public class ForeignKeyTestResult
    {
        public int MatchCount { get; set; }
        public double MatchRatio { get; set; }
        public string BestTargetField { get; set; } = "";
        public bool IsViable => MatchRatio > 0.1; // Threshold for considering it a potential FK
    }
}