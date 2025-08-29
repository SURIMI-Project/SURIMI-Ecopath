using ControlledVocabularies.Common;
using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Inference.Field;
using ControlledVocabularies.Inference.Strategies;
using ControlledVocabularies.Match;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;
using System.Linq;

namespace ControlledVocabularies.Inference
{
    /// <summary>
    /// Enhanced vocabulary semantic inferrer with field importance weighting and hierarchical analysis
    /// </summary>
    /// <todo>Cache unresolved FK candidates for future vocabulary arrivals</todo>
    /// <todo>Re-evaluate pending fields when new vocabularies registered</todo>
    /// <todo>Add spatial/temporal context field detection</todo>
    /// <todo>Implement brute-force FK discovery across all vocabularies</todo>
    public sealed class VocabularyInferenceEngine
    {
        private readonly VocabularyRegistry _registry;
        private readonly KeyFieldDescriptorIndexer _indexer;
        private readonly VocabularyAnalysisOrchestrator _orchestrator;

        public VocabularyInferenceEngine(IVocabularyRegistry? registry = null)
        {
            _indexer = new KeyFieldDescriptorIndexer(registry);
            _orchestrator = new VocabularyAnalysisOrchestrator();
            _registry = GlobalServiceLocator.Get<VocabularyRegistry>()!;

            // Register vocabulary-level strategies
            _orchestrator.Register(new DomainFromNameStrategy(registry));
            _orchestrator.Register(new FieldHintsCollectorStrategy());  // pulls from FieldInferenceInfo
            _orchestrator.Register(new FleetSegmentSignalStrategy());
            _orchestrator.Register(new ForeignKeyByOverlapStrategy(registry));
            // _orchestrator.Register(new ForeignKeyByMatcherStrategy(registry)); // optional
        }

        public SemanticInferenceResult AnalyzeVocabulary(IControlledVocabulary vocab)
        {
            // 0) Ensure descriptors exist (index if missing) – reuse KeyFieldIndexer
            foreach (var fn in vocab.FieldNames)
            {
                var d = vocab.GetKeyFieldDescriptor(fn);
                if (d == null)
                {
                    var temp = new Descriptors.KeyFieldDescriptor(fn, KeyDomain.NotSet, KeyPurpose.NotSet);
                    _indexer.BuildIndex(fn, vocab.Records, temp);
                }
            }

            // 1) Build per-field info with your existing AnalyzeFieldEnhanced
            var cache = new System.Collections.Generic.Dictionary<string, FieldInferenceInfo>(System.StringComparer.Ordinal);
            foreach (var fn in vocab.FieldNames)
            {
                var info = AnalyzeFieldEnhanced(vocab, fn); // your existing method
                cache[Utils.FieldPolicy.ForSchema(fn)] = info;
            }

            FieldInferenceInfo Provider(string name)
            {
                var key = Utils.FieldPolicy.ForSchema(name);
                FieldInferenceInfo v;
                if (cache.TryGetValue(key, out v)) return v;
                return new FieldInferenceInfo(name);
            }

            // 2) Run strategies then finalize with your rewritten InferPrimarySemantics
            var ctx = GlobalServiceLocator.Get<ModelContext>();
            return _orchestrator.Analyze(vocab, ctx, Provider, InferPrimarySemantics);
        }

        /// <summary>
        /// Enhanced field analysis with importance weighting, descriptor integration, and hierarchical detection
        /// </summary>
        private FieldInferenceInfo AnalyzeFieldEnhanced(IControlledVocabulary vocab, string fieldName)
        {
            var fieldInfo = new FieldInferenceInfo(fieldName);

            // Get sample values for analysis
            var sampleValues = FieldFilter.ExtractFieldValues(vocab.Records, fieldName);
            if (!sampleValues.Any())
            {
                fieldInfo.AddDiagnostic($"No sample values found for field '{fieldName}'");
                return fieldInfo;
            }

            // Get or create field descriptor for enhanced analysis
            var descriptor = GetOrCreateFieldDescriptor(vocab, fieldName, sampleValues);
            fieldInfo.SetDescriptor(descriptor);

            // Phase 1: Importance weighting analysis
            AnalyzeFieldImportance(fieldName, sampleValues, descriptor, fieldInfo);

            // Phase 2: Integrate KeyFieldDescriptor insights
            IntegrateDescriptorInsights(descriptor, fieldInfo);

            // Hierarchical nesting analysis for code fields
            if (descriptor.Kind == FieldKind.Code)
            {
                AnalyzeHierarchicalNesting(sampleValues, fieldInfo);
            }

            // Semantic inference from field patterns using registry
            InferFieldSemantics(vocab, fieldName, sampleValues, descriptor, fieldInfo);

            return fieldInfo;
        }

        /// <summary>
        /// Phase 1: Analyze field importance with weighting hierarchy
        /// </summary>
        private void AnalyzeFieldImportance(string fieldName, List<string> sampleValues, KeyFieldDescriptor descriptor, FieldInferenceInfo fieldInfo)
        {
            var fieldNameLower = fieldName.ToLowerInvariant();

            // Name fields (highest importance) - the semantic goldmine!
            if (IsNameField(fieldNameLower))
            {
                fieldInfo.ImportanceWeight = FieldImportanceWeight.Name;
                fieldInfo.AddReason($"Name field detected - highest semantic importance");

                // Name fields are excellent domain/purpose indicators
                if (fieldNameLower.Contains("scientific") || fieldNameLower.Contains("binomial"))
                {
                    fieldInfo.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.95, "Scientific name indicates species domain");
                }
                else if (fieldNameLower.Contains("common") || fieldNameLower.Contains("vernacular"))
                {
                    fieldInfo.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.85, "Common name indicates species domain");
                }
                else if (fieldNameLower.Contains("gear") && fieldNameLower.Contains("name"))
                {
                    fieldInfo.AddSemanticHint(KeyDomain.FleetSegment, KeyPurpose.Gear, 0.9, "Gear name indicates fleet domain");
                    // add parallel Fleet hint
                    fieldInfo.AddSemanticHint(KeyDomain.FleetSegment, KeyPurpose.Fleet, 0.8, "Gear is part of fleet segmentation");
                }
            }

            // Code fields (medium importance) - identifiers and potential FKs
            else if (IsCodeField(fieldNameLower) || descriptor.Kind == FieldKind.Code)
            {
                fieldInfo.ImportanceWeight = FieldImportanceWeight.Code;
                fieldInfo.IsPotentialForeignKey = true;
                fieldInfo.ForeignKeyConfidence = CalculateCodeFieldFKConfidence(sampleValues, descriptor);
                fieldInfo.AddReason($"Code field detected - potential foreign key (confidence: {fieldInfo.ForeignKeyConfidence:F2})");
            }

            // Description fields (lower importance) - additional context
            else if (IsDescriptionField(fieldNameLower))
            {
                fieldInfo.ImportanceWeight = FieldImportanceWeight.Description;
                fieldInfo.AddReason("Description field - provides context but less semantic weight");
            }

            // Context fields (medium importance) - spatial/temporal/metadata
            else if (IsContextField(fieldNameLower))
            {
                fieldInfo.ImportanceWeight = FieldImportanceWeight.Context;
                AnalyzeContextField(fieldNameLower, sampleValues, fieldInfo);
            }

            // Unknown fields (lowest importance)
            else
            {
                fieldInfo.ImportanceWeight = FieldImportanceWeight.Unknown;
                fieldInfo.AddReason("Field purpose unclear - requires content analysis");
            }
        }

        /// <summary>
        /// Phase 2: Integrate insights from KeyFieldDescriptor
        /// </summary>
        private void IntegrateDescriptorInsights(KeyFieldDescriptor descriptor, FieldInferenceInfo fieldInfo)
        {
            // Use descriptor statistics for semantic clues
            if (descriptor.UniquenessRatio > 0.95)
            {
                fieldInfo.AddSemanticHint(KeyDomain.NotSet, KeyPurpose.NotSet, 0.8,
                    $"Highly unique field (ratio: {descriptor.UniquenessRatio:F2}) - likely identifier");
                fieldInfo.IsPotentialForeignKey = true;
            }

            if (descriptor.NonZeroRatio < 0.5)
            {
                fieldInfo.AddDiagnostic($"Sparse field (coverage: {descriptor.NonZeroRatio:F2}) - may be optional metadata");
                fieldInfo.ImportanceWeight = fieldInfo.ImportanceWeight.LowerBySparsity();
            }

            // Strategy patterns reveal semantic intent
            if (descriptor.Strategy.HasFlag(MatchStrategy.Exact) && descriptor.Strategy.HasFlag(MatchStrategy.Fuzzy))
            {
                fieldInfo.AddReason("Exact+Fuzzy strategy suggests name field for matching");
            }
            else if (descriptor.Strategy == MatchStrategy.Exact && descriptor.Kind == FieldKind.Code)
            {
                fieldInfo.AddReason("Exact-only code field suggests foreign key or identifier");
                fieldInfo.ForeignKeyConfidence = Math.Max(fieldInfo.ForeignKeyConfidence, 0.7);
            }
        }

        /// <summary>
        /// Analyze hierarchical nesting in code field values
        /// </summary>
        private void AnalyzeHierarchicalNesting(List<string> sampleValues, FieldInferenceInfo fieldInfo)
        {
            var nestingAnalysis = CalculateHierarchicalNesting(sampleValues);
            fieldInfo.SetHierarchicalNesting(nestingAnalysis);

            if (nestingAnalysis.IsHierarchical)
            {
                fieldInfo.AddReason($"Hierarchical structure detected - depth: {nestingAnalysis.MaxDepth}, " +
                                  $"separators: [{string.Join(", ", nestingAnalysis.Separators)}]");

                // Hierarchical fields are usually NOT primary keys
                if (nestingAnalysis.MaxDepth > 2)
                {
                    fieldInfo.AddDiagnostic("Deep hierarchy suggests composite/derived key rather than primary key");
                }

                // But they're excellent for taxonomic/geographic context
                if (nestingAnalysis.Separators.Contains('.'))
                {
                    fieldInfo.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.6,
                        "Dot-separated hierarchy suggests taxonomic classification");
                }
            }
        }

        /// <summary>
        /// Enhanced vocabulary domain inference from name patterns using registry
        /// </summary>
        private void InferVocabularyDomainFromName(string vocabName, SemanticInferenceResult result)
        {
            var normalizedName = FieldPolicy.ForSchema(vocabName);

            // Dynamic vocabulary detection using registry
            if (_registry != null)
            {
                foreach (var existingVocab in _registry.GetAll())
                {
                    var normalizedExistingName = FieldPolicy.ForSchema(existingVocab.VocabularyName);

                    if (normalizedName.Contains(normalizedExistingName) || normalizedExistingName.Contains(normalizedName))
                    {
                        result.AddDomainHint(existingVocab.Domain, 0.8,
                            $"Vocabulary name similar to existing '{existingVocab.VocabularyName}' vocabulary");
                        result.AddPurposeHint(existingVocab.Purpose, 0.8,
                            $"Purpose inferred from similar vocabulary '{existingVocab.VocabularyName}'");
                    }
                }
            }

            // Fallback patterns for common domain indicators
            var name = normalizedName;

            // Species domain patterns
            if (name.Contains("species") || name.Contains("fish") || name.Contains("marine") ||
                name.Contains("taxon") || name.Contains("biological"))
            {
                result.AddDomainHint(KeyDomain.Species, 0.7, "Vocabulary name suggests species domain");
            }

            // Fleet/fishing domain patterns  
            if (name.Contains("gear") || name.Contains("fishing") || name.Contains("fleet") ||
                name.Contains("vessel"))
            {
                result.AddDomainHint(KeyDomain.FleetSegment, 0.8, "Vocabulary name suggests fishing domain");
            }

            // Geographic domain patterns
            if (name.Contains("country") || name.Contains("nation") || name.Contains("region") ||
                name.Contains("geographic") || name.Contains("spatial"))
            {
                result.AddDomainHint(KeyDomain.Country, 0.8, "Vocabulary name suggests geographic domain");
            }

            // Life stage domain patterns
            if (name.Contains("lifestage") || name.Contains("stage") || name.Contains("life"))
            {
                result.AddDomainHint(KeyDomain.Species, 0.7, "Vocabulary name suggests species domain");
                result.AddPurposeHint(KeyPurpose.Lifestage, 0.9, "Vocabulary name suggests lifestage purpose");
            }

            /// <todo>Add patterns for new context domains (Geographic, Temporal, Environmental, Metadata)</todo>
        }

        /// <summary>
        /// Enhanced field semantic inference using registry-based pattern matching
        /// </summary>
        private void InferFieldSemantics(IControlledVocabulary sourceVocab, string fieldName, List<string> sampleValues,
            KeyFieldDescriptor descriptor, FieldInferenceInfo fieldInfo)
        {
            if (_registry == null || !sampleValues.Any()) return;

            // Test against all known purposes using existing vocabulary matching
            var purposesToTest = new[]
            {
                KeyPurpose.Species, KeyPurpose.Lifestage, KeyPurpose.Gear,
                KeyPurpose.Country, KeyPurpose.Age, KeyPurpose.Length
                /// <todo>Add new context purposes when implemented</todo>
            };

            foreach (var purpose in purposesToTest)
            {
                if (IsSemanticPattern(sourceVocab, fieldName, sampleValues, purpose))
                {
                    var confidence = CalculateSemanticConfidence(fieldName, sampleValues, purpose);
                    var domain = InferDomainFromPurpose(purpose);

                    fieldInfo.AddSemanticHint(domain, purpose, confidence,
                        $"Field content matches existing {purpose} vocabulary patterns");
                }
            }

            // Additional field name-based hints
            InferFromFieldName(fieldName, fieldInfo);
        }

        /// <summary>
        /// Test if field content matches existing vocabularies with specific purpose
        /// </summary>
        private bool IsSemanticPattern(IControlledVocabulary sourceVocab, string fieldName, List<string> sampleValues,
            KeyPurpose targetPurpose, int? minScore = null)
        {
            var threshold = minScore ?? LocalSettings.DefaultMinScore;
            var targetVocabs = _registry!.GetByPurpose(targetPurpose);
            var matcher = new GenericVocabularyMatcher(_registry);

            foreach (var targetVocab in targetVocabs)
            {
                // Test with first few sample values
                foreach (var sampleValue in sampleValues.Take(3))
                {
                    var testKey = MultiLevelKey.FromPairs([(fieldName, sampleValue)], targetVocab.Domain);
                    var result = matcher.Match(testKey, sourceVocab, targetVocab);

                    if (result.Score >= threshold)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculate semantic confidence based on field name and content patterns
        /// </summary>
        private double CalculateSemanticConfidence(string fieldName, List<string> sampleValues, KeyPurpose purpose)
        {
            double confidence = 0.6; // Base confidence for matching content

            // Boost confidence if field name also suggests the purpose
            var fieldNameLower = fieldName.ToLowerInvariant();
            if (purpose == KeyPurpose.Species && (fieldNameLower.Contains("species") || fieldNameLower.Contains("scientific")))
                confidence += 0.2;
            else if (purpose == KeyPurpose.Lifestage && fieldNameLower.Contains("stage"))
                confidence += 0.3;
            else if (purpose == KeyPurpose.Gear && fieldNameLower.Contains("gear"))
                confidence += 0.3;
            else if (purpose == KeyPurpose.Country && (fieldNameLower.Contains("country") || fieldNameLower.Contains("flag")))
                confidence += 0.3;

            return Math.Min(1.0, confidence);
        }

        /// <summary>
        /// Infer likely domain from purpose
        /// </summary>
        private KeyDomain InferDomainFromPurpose(KeyPurpose purpose)
        {
            return purpose switch
            {
                KeyPurpose.Species or KeyPurpose.Lifestage or KeyPurpose.Age or KeyPurpose.Length => KeyDomain.Species,
                KeyPurpose.Gear or KeyPurpose.Fleet => KeyDomain.FleetSegment,
                KeyPurpose.Country => KeyDomain.Country,
                KeyPurpose.Market => KeyDomain.Market,
                /// <todo>Add mappings for new context purposes</todo>
                _ => KeyDomain.NotSet
            };
        }

        /// <summary>
        /// Enhanced primary semantics inference using weighted field analysis
        /// </summary>
        private void InferPrimarySemantics(SemanticInferenceResult result)
        {
            // Accumulators
            var domainVotes = new Dictionary<KeyDomain, double>();
            var purposeVotes = new Dictionary<KeyPurpose, double>();

            // --- 1) Collect votes from field-level hints (weighted) ---
            foreach (var field in result.FieldInferences)
            {
                // Importance -> [0.2 .. 1.0]  (Unknown=0.2, Name=1.0)
                double importanceMultiplier = (double)((int)field.ImportanceWeight) / 5.0;
                if (importanceMultiplier < 0.0) importanceMultiplier = 0.0;
                if (importanceMultiplier > 1.0) importanceMultiplier = 1.0;

                // Field-level reliability (0..1) — already combines stats/hierarchy
                double fieldReliability = field.OverallConfidence;
                if (fieldReliability < 0.0) fieldReliability = 0.0;
                if (fieldReliability > 1.0) fieldReliability = 1.0;

                // Base weight for this field’s hints
                double baseWeight = importanceMultiplier * (0.5 + 0.5 * fieldReliability);

                // Fleet/Metier tagging: boost FleetSegment detection signals
                // Normalize name & reasons via FieldPolicy
                string fname = FieldPolicy.ForSchema(field.FieldName);
                bool hasFleetTags = ContainsFleetTags(fname);

                // Also scan reasons for tags (plain loop; no LINQ)
                if (!hasFleetTags)
                {
                    for (int i = 0; i < field.Reasons.Count; i++)
                    {
                        string rn = FieldPolicy.ForSchema(field.Reasons[i] ?? "");
                        if (ContainsFleetTags(rn)) { hasFleetTags = true; break; }
                    }
                }

                // Apply hints
                for (int i = 0; i < field.SemanticHints.Count; i++)
                {
                    var hint = field.SemanticHints[i];

                    // Vote contributed by this hint
                    double vote = hint.Confidence * baseWeight;

                    // Extra nudge if fleet/metier tags are present and hint relates to fleet/gear
                    if (hasFleetTags)
                    {
                        if (hint.Domain == KeyDomain.FleetSegment) vote += 0.10; // small domain nudge
                        if ((hint.Purpose & KeyPurpose.Gear) != 0UL ||
                            (hint.Purpose & KeyPurpose.Fleet) != 0UL)
                        {
                            vote += 0.10; // small purpose nudge
                        }
                    }

                    // Accumulate domain vote
                    double dv;
                    if (!domainVotes.TryGetValue(hint.Domain, out dv)) dv = 0.0;
                    dv += vote;
                    domainVotes[hint.Domain] = dv;

                    // Accumulate purpose vote
                    double pv;
                    if (!purposeVotes.TryGetValue(hint.Purpose, out pv)) pv = 0.0;
                    pv += vote;
                    purposeVotes[hint.Purpose] = pv;
                }
            }

            // --- 2) Blend in vocabulary-level hints (name/registry heuristics) ---
            // Domain
            foreach (var dh in result.DomainHints)
            {
                double v;
                if (!domainVotes.TryGetValue(dh.Domain, out v)) v = 0.0;
                v += dh.Confidence;
                domainVotes[dh.Domain] = v;
            }
            // Purpose
            foreach (var ph in result.PurposeHints)
            {
                double v;
                if (!purposeVotes.TryGetValue(ph.Purpose, out v)) v = 0.0;
                v += ph.Confidence;
                purposeVotes[ph.Purpose] = v;
            }

            // --- 3) Decide primary Domain (normalize by SUM, not by bucket count) ---
            KeyDomain topDomain = KeyDomain.NotSet;
            double topDomainValue = 0.0;
            double domainSum = 0.0;

            // Classic loop; no LINQ
            foreach (var kv in domainVotes)
            {
                domainSum += kv.Value;
                if (kv.Value > topDomainValue)
                {
                    topDomainValue = kv.Value;
                    topDomain = kv.Key;
                }
            }

            if (domainSum > 0.0)
            {
                result.InferredDomain = topDomain;
                result.DomainConfidence = topDomainValue / domainSum;
                if (result.DomainConfidence > 1.0) result.DomainConfidence = 1.0;
            }
            else
            {
                result.InferredDomain = KeyDomain.NotSet;
                result.DomainConfidence = 0.0;
            }

            // --- 4) Decide Purpose(s) (flags). Pick all within a fraction of the top ---
            KeyPurpose topPurposeKey = KeyPurpose.NotSet;
            double topPurposeValue = 0.0;

            foreach (var kv in purposeVotes)
            {
                if (kv.Value > topPurposeValue)
                {
                    topPurposeValue = kv.Value;
                    topPurposeKey = kv.Key;
                }
            }

            // If we have any purpose votes, include all >= threshold * top
            result.InferredPurpose = KeyPurpose.NotSet;
            if (topPurposeValue > 0.0)
            {
                const double FRACTION = 0.30; // include any purpose with ≥30% of top
                double cutoff = topPurposeValue * FRACTION;

                foreach (var kv in purposeVotes)
                {
                    if (kv.Value >= cutoff)
                    {
                        result.InferredPurpose |= kv.Key;
                    }
                }

                // Safety: if nothing passed the cutoff (shouldn't happen), choose the top
                if (result.InferredPurpose == KeyPurpose.NotSet)
                {
                    result.InferredPurpose = topPurposeKey;
                }
            }

            // --- 5) Complementary purpose logic for Fleet/Metier signals ---
            // If domain is FleetSegment and Gear is selected, also include Fleet.
            if (result.InferredDomain == KeyDomain.FleetSegment)
            {
                if ((result.InferredPurpose & KeyPurpose.Gear) != 0UL)
                {
                    result.InferredPurpose |= KeyPurpose.Fleet;
                }
            }
        }

        /// <summary>
        /// Enhanced foreign key hypothesis testing with registry integration
        /// </summary>
        private void TestForeignKeyHypotheses(IControlledVocabulary sourceVocab, SemanticInferenceResult result)
        {
            var potentialFKFields = result.FieldInferences
                .Where(f => f.IsPotentialForeignKey && f.ForeignKeyConfidence > 0.3)
                .OrderByDescending(f => f.ForeignKeyConfidence)
                .ToList();

            foreach (var fkField in potentialFKFields)
            {
                // Test against compatible vocabularies first (smarter targeting)
                var compatibleVocabs = _registry!.GetByDomain(result.InferredDomain).ToList();
                if (!compatibleVocabs.Any())
                {
                    // Fallback to all vocabularies if no domain-compatible ones found
                    /// <todo>Implement brute-force FK discovery across all vocabularies when needed</todo>
                    compatibleVocabs = _registry.GetAll().ToList();
                }

                TestFieldAsForeignKey(sourceVocab, fkField, compatibleVocabs, result);
            }
        }

        /// <summary>
        /// Test a field as foreign key against target vocabularies
        /// </summary>
        private void TestFieldAsForeignKey(IControlledVocabulary sourceVocab, FieldInferenceInfo fkField,
            IEnumerable<IControlledVocabulary> targetVocabs, SemanticInferenceResult result)
        {
            var sourceFieldName = fkField.FieldName;

            foreach (var targetVocab in targetVocabs)
            {
                if (ReferenceEquals(sourceVocab, targetVocab)) continue;

                var fkTest = TestForeignKeyMatch(sourceVocab, sourceFieldName, targetVocab);
                if (fkTest.IsViable)
                {
                    var fkResult = new ForeignKeyMatchResult
                    {
                        SourceField = sourceFieldName,
                        SourceVocabulary = sourceVocab.VocabularyName,
                        TargetVocabulary = targetVocab.VocabularyName,
                        TargetField = fkTest.BestTargetField,
                        Score = (int)(fkTest.MatchRatio * 100), // Convert ratio to 0-100 integer
                        StrategyUsed = MatchStrategy.Exact,
                        Justification = GenerateFKReasoning(fkField, fkTest, targetVocab),
                        MatchCount = fkTest.MatchCount,
                        Confidence = fkTest.MatchRatio * fkField.ForeignKeyConfidence
                    };

                    result.AddForeignKeyCandidate(fkResult);
                }
            }
        }
        /// <summary>
        /// Calculate hierarchical nesting metrics for code values
        /// </summary>
        private HierarchicalNestingAnalysis CalculateHierarchicalNesting(List<string> values)
        {
            var analysis = new HierarchicalNestingAnalysis();
            var commonSeparators = new[] { '.', '-', '_', ':', '/', '\\' };
            var separatorCounts = new Dictionary<char, int>();
            var depthCounts = new Dictionary<int, int>();

            foreach (var value in values.Take(50)) // Sample for performance
            {
                if (string.IsNullOrWhiteSpace(value)) continue;

                foreach (var sep in commonSeparators)
                {
                    if (value.Contains(sep))
                    {
                        separatorCounts[sep] = separatorCounts.GetValueOrDefault(sep) + 1;
                        var depth = value.Count(c => c == sep) + 1;
                        depthCounts[depth] = depthCounts.GetValueOrDefault(depth) + 1;
                        analysis.MaxDepth = Math.Max(analysis.MaxDepth, depth);
                    }
                }
            }

            // Consider hierarchical if >30% of values have consistent separator usage
            var totalValues = values.Count;
            var threshold = totalValues * 0.3;

            analysis.Separators = separatorCounts.Where(kv => kv.Value > threshold).Select(kv => kv.Key).ToList();
            analysis.IsHierarchical = analysis.Separators.Any() && analysis.MaxDepth > 1;
            analysis.ConsistencyRatio = analysis.Separators.Any()
                ? separatorCounts.Where(kv => analysis.Separators.Contains(kv.Key)).Sum(kv => kv.Value) / (double)totalValues
                : 0.0;

            return analysis;
        }

        /// <summary>
        /// Get existing field descriptor or create one with indexing for enhanced analysis
        /// </summary>
        private KeyFieldDescriptor GetOrCreateFieldDescriptor(IControlledVocabulary vocab, string fieldName, List<string> sampleValues)
        {
            // Try to get existing descriptor first
            var existing = vocab.GetKeyFieldDescriptor(fieldName);
            if (existing != null) return existing;

            // Create temporary descriptor and index it for statistics
            var tempDescriptor = new KeyFieldDescriptor(fieldName, KeyDomain.NotSet, KeyPurpose.NotSet);

            // Use field indexer to build statistics
            _indexer.BuildIndex(fieldName, vocab.Records, tempDescriptor);

            return tempDescriptor;
        }

        /// <summary>
        /// Test foreign key match between source field and target vocabulary
        /// </summary>
        private ForeignKeyTestResult TestForeignKeyMatch(IControlledVocabulary source, string sourceField, IControlledVocabulary target)
        {
            var result = new ForeignKeyTestResult();
            var sourceValues = FieldFilter.ExtractFieldValues(source.Records, sourceField)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => FieldPolicy.ForValue(v, FieldKind.Code)) // Normalize for comparison
                .ToHashSet();

            if (!sourceValues.Any()) return result;

            // Test against each target field, prioritizing code fields
            var targetFields = target.FieldNames.OrderBy(f =>
                target.GetKeyFieldDescriptor(f)?.Kind == FieldKind.Code ? 0 : 1).ToList();

            foreach (var targetField in targetFields)
            {
                var targetValues = FieldFilter.ExtractFieldValues(target.Records, targetField)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => FieldPolicy.ForValue(v, FieldKind.Code))
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

        // Helper methods for field classification
        private bool IsNameField(string fieldNameLower) =>
            fieldNameLower.Contains("name") || fieldNameLower.Contains("label") ||
            fieldNameLower.Contains("title") || fieldNameLower.Contains("scientific") ||
            fieldNameLower.Contains("common") || fieldNameLower.Contains("vernacular");

        private bool IsCodeField(string fieldNameLower) =>
            fieldNameLower.Contains("code") || fieldNameLower.Contains("id") ||
            fieldNameLower.Contains("identifier") || fieldNameLower.Contains("key");

        private bool IsDescriptionField(string fieldNameLower) =>
            fieldNameLower.Contains("description") || fieldNameLower.Contains("comment") ||
            fieldNameLower.Contains("note") || fieldNameLower.Contains("remark") ||
            fieldNameLower.Contains("detail");

        private bool IsContextField(string fieldNameLower) =>
            // Spatial context
            fieldNameLower.Contains("region") || fieldNameLower.Contains("area") ||
            fieldNameLower.Contains("location") || fieldNameLower.Contains("geographic") ||
            fieldNameLower.Contains("latitude") || fieldNameLower.Contains("longitude") ||
            // Temporal context  
            fieldNameLower.Contains("year") || fieldNameLower.Contains("date") ||
            fieldNameLower.Contains("time") || fieldNameLower.Contains("period") ||
            // Metadata context
            fieldNameLower.Contains("version") || fieldNameLower.Contains("created") ||
            fieldNameLower.Contains("modified") || fieldNameLower.Contains("updated");

        private void InferFromFieldName(string fieldName, FieldInferenceInfo fieldInfo)
        {
            var fieldNameLower = fieldName.ToLowerInvariant();

            // Additional field name-based semantic hints
            if (fieldNameLower.Contains("taxonomy") || fieldNameLower.Contains("taxon"))
                fieldInfo.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.8, "Taxonomic field name");

            if (fieldNameLower.Contains("depth") || fieldNameLower.Contains("habitat"))
                fieldInfo.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.6, "Species habitat field");
        }

        private void AnalyzeContextField(string fieldNameLower, List<string> sampleValues, FieldInferenceInfo fieldInfo)
        {
            // Spatial context detection
            if (fieldNameLower.Contains("region") || fieldNameLower.Contains("area"))
            {
                fieldInfo.AddSemanticHint(KeyDomain.Geographic, KeyPurpose.SpatialExtent, 0.8, "Geographic context field");
            }
            else if (fieldNameLower.Contains("year") || fieldNameLower.Contains("date"))
            {
                fieldInfo.AddSemanticHint(KeyDomain.Temporal, KeyPurpose.TimeStamp, 0.8, "Temporal context field");
            }
            /// <todo>Add more context field patterns as needed</todo>
        }

        private double CalculateCodeFieldFKConfidence(List<string> sampleValues, KeyFieldDescriptor descriptor)
        {
            double confidence = 0.5; // Base confidence for code fields

            // Short, unique codes are more likely FKs
            if (descriptor.AvgLength <= 5 && descriptor.UniquenessRatio > 0.8)
                confidence += 0.3;

            // High distinctness suggests referential usage
            if (descriptor.UniquenessRatio > 0.95)
                confidence += 0.2;

            return Math.Min(1.0, confidence);
        }

        private string GenerateFKReasoning(FieldInferenceInfo fkField, ForeignKeyTestResult fkTest, IControlledVocabulary targetVocab) =>
            $"Field '{fkField.FieldName}' matches {fkTest.MatchCount} values ({fkTest.MatchRatio:P1}) " +
            $"with '{targetVocab.VocabularyName}.{fkTest.BestTargetField}' " +
            $"(confidence: {fkField.ForeignKeyConfidence:F2})";

        /// <summary>
        /// Helper class for FK testing results  
        /// </summary>
        private class ForeignKeyTestResult
        {
            public int MatchCount { get; set; }
            public double MatchRatio { get; set; }
            public string BestTargetField { get; set; } = "";
            public bool IsViable
            {
                get
                {
                    // viable if ≥ 3 direct matches OR ≥10% ratio
                    return MatchCount >= 3 || MatchRatio > 0.10;
                }
            }
        }

        // Helper: detects fleet-related tags in a normalized string
        private static bool ContainsFleetTags(string normalized)
        {
            // normalized is already FieldPolicy.ForSchema(text), so dashes separate tokens
            // We look for substrings that commonly indicate fleet/metier data.
            if (string.IsNullOrEmpty(normalized)) return false;

            // Tags: fleet, gear, vessel, metier, métier (normalized), métier-code, segment
            // Use ordinal checks; no culture deps
            if (normalized.IndexOf("fleet", StringComparison.Ordinal) >= 0) return true;
            if (normalized.IndexOf("gear", StringComparison.Ordinal) >= 0) return true;
            if (normalized.IndexOf("vessel", StringComparison.Ordinal) >= 0) return true;
            if (normalized.IndexOf("metier", StringComparison.Ordinal) >= 0) return true;   // covers “métier” once normalized
            if (normalized.IndexOf("segment", StringComparison.Ordinal) >= 0) return true;

            return false;
        }
    }
}