// ControlledVocabularies.Inference.Vocabulary/VocabularyInferenceEngine.cs
using ControlledVocabularies.Common;
using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Inference;
using ControlledVocabularies.Inference.Field;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;
using System;

namespace ControlledVocabularies.Inference.Vocabulary
{
    /// <summary>
    /// No-LINQ vocabulary-level inferrer/engine.
    /// - Clear loops (no LINQ, no lambdas)
    /// - Reuses FieldFilter for sampling
    /// - Reuses KeyFieldDescriptorIndexer for descriptor stats
    /// </summary>
    public sealed class VocabularyInferenceEngine
    {
        private readonly IVocabularyRegistry? _registry;
        private readonly KeyFieldDescriptorIndexer _fieldIndexer;

        public VocabularyInferenceEngine(IVocabularyRegistry? registry = null)
        {
            _registry = registry;
            _fieldIndexer = new KeyFieldDescriptorIndexer(registry);
        }

        public SemanticInferenceResult Analyze(IControlledVocabulary vocabulary)
        {
            var result = new SemanticInferenceResult(vocabulary.VocabularyName);

            // 1) Hints from vocabulary name
            InferVocabularyDomainFromName(vocabulary.VocabularyName, result);

            // 2) Field-by-field analysis (enhanced; uses descriptor stats)
            foreach (var fieldName in vocabulary.FieldNames)
            {
                var fieldInfo = AnalyzeFieldEnhanced(vocabulary, fieldName);
                result.AddFieldInference(fieldInfo);
            }

            // 3) Primary semantics from weighted field hints + name hints
            InferPrimarySemantics(result);

            // 4) FK hypotheses if registry available
            if (_registry != null)
            {
                TestForeignKeyHypotheses(vocabulary, result);
            }
            else
            {
                result.AddDiagnostic("Registry unavailable – FK discovery deferred.");
            }

            return result;
        }

        // ---------------------------
        // Field analysis (no LINQ)
        // ---------------------------
        private FieldInferenceInfo AnalyzeFieldEnhanced(IControlledVocabulary vocab, string fieldName)
        {
            var info = new FieldInferenceInfo(fieldName);

            // Samples (raw, de-duplicated per FieldFilter + LocalSettings)
            var samples = FieldFilter.ExtractFieldValues(vocab.Records, fieldName);
            if (samples.Count == 0)
            {
                info.AddDiagnostic("No sample values for field.");
                return info;
            }

            // Descriptor: use existing or build a temporary one via indexer
            var descriptor = GetOrCreateDescriptor(vocab, fieldName, samples);
            info.SetDescriptor(descriptor);

            // Importance (names > codes > context > desc > unknown)
            AnalyzeImportance(fieldName, samples, descriptor, info);

            // Hierarchical signal for codes (structural insight)
            if (descriptor.Kind == FieldKind.Code)
            {
                var nesting = CalculateHierarchicalNesting(samples);
                info.SetHierarchicalNesting(nesting);
                if (nesting.IsHierarchical)
                {
                    info.AddReason("Hierarchical code structure detected.");
                    if (IndexOfChar(nesting.Separators, '.') >= 0)
                    {
                        info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.6,
                            "Dot-separated hierarchy – taxonomic hint.");
                    }
                }
            }

            // Name/label strategy hints
            if (descriptor.Strategy == (MatchStrategy.Exact | MatchStrategy.Fuzzy))
            {
                info.AddReason("Exact+Fuzzy strategy – likely name/label.");
            }
            else if (descriptor.Strategy == MatchStrategy.Exact && descriptor.Kind == FieldKind.Code)
            {
                info.IsPotentialForeignKey = true;
                if (info.ForeignKeyConfidence < 0.7) info.ForeignKeyConfidence = 0.7;
                info.AddReason("Exact-only code – likely identifier/foreign key.");
            }

            // Registry-powered semantic nudges (fast pass)
            InferFieldSemanticsFromName(fieldName, info);

            return info;
        }

        private KeyFieldDescriptor GetOrCreateDescriptor(IControlledVocabulary vocab, string fieldName, System.Collections.Generic.List<string> samples)
        {
            var existing = vocab.GetKeyFieldDescriptor(fieldName);
            if (existing != null) return existing;

            var temp = new KeyFieldDescriptor(fieldName, KeyDomain.NotSet, KeyPurpose.NotSet, false, 0, MatchStrategy.None);
            // Build statistics/kind/strategy/weight via indexer
            _fieldIndexer.BuildIndex(fieldName, vocab.Records, temp);
            return temp;
        }

        private void AnalyzeImportance(string fieldName, System.Collections.Generic.List<string> samples, KeyFieldDescriptor descriptor, FieldInferenceInfo info)
        {
            var lname = fieldName.ToLowerInvariant();

            if (IsNameField(lname))
            {
                info.ImportanceWeight = FieldImportanceWeight.Name;
                info.AddReason("Name field – high semantic importance.");

                if (Contains(lname, "scientific") || Contains(lname, "binomial"))
                    info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.95, "Scientific name.");
                else if (Contains(lname, "common") || Contains(lname, "vernacular"))
                    info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.85, "Common name.");
                else if (Contains(lname, "gear") && Contains(lname, "name"))
                    info.AddSemanticHint(KeyDomain.FleetSegment, KeyPurpose.Gear, 0.9, "Gear name.");
                return;
            }

            if (IsCodeField(lname) || descriptor.Kind == FieldKind.Code)
            {
                info.ImportanceWeight = FieldImportanceWeight.Code;
                info.IsPotentialForeignKey = true;
                info.ForeignKeyConfidence = CalculateCodeFieldFKConfidence(samples, descriptor);
                info.AddReason("Code field – potential FK.");
                return;
            }

            if (IsContextField(lname))
            {
                info.ImportanceWeight = FieldImportanceWeight.Context;
                AnalyzeContextField(lname, samples, info);
                return;
            }

            if (IsDescriptionField(lname))
            {
                info.ImportanceWeight = FieldImportanceWeight.Description;
                info.AddReason("Description context field.");
                return;
            }

            info.ImportanceWeight = FieldImportanceWeight.Unknown;
            info.AddReason("Purpose unclear – needs content analysis.");
        }

        // ---------------------------
        // Vocabulary name hints
        // ---------------------------
        private void InferVocabularyDomainFromName(string vocabName, SemanticInferenceResult result)
        {
            var name = FieldPolicy.ForSchema(vocabName);

            // Registry similarity
            if (_registry != null)
            {
                foreach (var v in _registry.GetAll())
                {
                    var other = FieldPolicy.ForSchema(v.VocabularyName);
                    if (Contains(name, other) || Contains(other, name))
                    {
                        result.AddDomainHint(v.Domain, 0.8, "Name similar to existing vocabulary.");
                        result.AddPurposeHint(v.Purpose, 0.8, "Purpose inferred by name similarity.");
                    }
                }
            }

            // Simple patterns
            if (Contains(name, "species") || Contains(name, "fish") || Contains(name, "marine") ||
                Contains(name, "taxon") || Contains(name, "biological"))
            {
                result.AddDomainHint(KeyDomain.Species, 0.7, "Name suggests species domain.");
            }

            if (Contains(name, "gear") || Contains(name, "fishing") || Contains(name, "fleet") || Contains(name, "vessel") || Contains(name, "metier"))
            {
                result.AddDomainHint(KeyDomain.FleetSegment, 0.8, "Name suggests fleet segment domain.");
                result.AddPurposeHint(KeyPurpose.Gear | KeyPurpose.Fleet, 0.6, "Gear/Fleet cues present.");
            }

            if (Contains(name, "country") || Contains(name, "nation") || Contains(name, "region") ||
                Contains(name, "geographic") || Contains(name, "spatial"))
            {
                result.AddDomainHint(KeyDomain.Country, 0.8, "Name suggests geographic/country domain.");
            }

            if (Contains(name, "lifestage") || Contains(name, "stage") || Contains(name, "life"))
            {
                result.AddDomainHint(KeyDomain.Species, 0.7, "Name hints species.");
                result.AddPurposeHint(KeyPurpose.Lifestage, 0.9, "Lifestage purpose.");
            }
        }

        // ---------------------------
        // Primary semantics (no LINQ)
        // ---------------------------
        private void InferPrimarySemantics(SemanticInferenceResult result)
        {
            var domainVotes = new System.Collections.Generic.Dictionary<KeyDomain, double>();
            var purposeVotes = new System.Collections.Generic.Dictionary<KeyPurpose, double>();

            // Field-driven votes (weighted)
            foreach (var field in result.FieldInferences)
            {
                double importance = (int)field.ImportanceWeight / 5.0;
                int i = 0;
                var hints = field.SemanticHints;
                var hintsCount = hints.Count;
                while (i < hintsCount)
                {
                    var hint = hints[i];
                    double weighted = hint.Confidence * importance;

                    double prev;
                    if (!domainVotes.TryGetValue(hint.Domain, out prev)) prev = 0.0;
                    domainVotes[hint.Domain] = prev + weighted;

                    double prevP;
                    if (!purposeVotes.TryGetValue(hint.Purpose, out prevP)) prevP = 0.0;
                    purposeVotes[hint.Purpose] = prevP + weighted;

                    i++;
                }
            }

            // Vocabulary name hints
            foreach (var dh in result.DomainHints)
            {
                double prev;
                if (!domainVotes.TryGetValue(dh.Domain, out prev)) prev = 0.0;
                domainVotes[dh.Domain] = prev + dh.Confidence;
            }
            foreach (var ph in result.PurposeHints)
            {
                double prev;
                if (!purposeVotes.TryGetValue(ph.Purpose, out prev)) prev = 0.0;
                purposeVotes[ph.Purpose] = prev + ph.Confidence;
            }

            // Pick domain with max vote; compute a simple confidence
            KeyDomain bestDomain = KeyDomain.NotSet;
            double bestDomainScore = -1.0;
            double domainTotal = 0.0;

            foreach (var kv in domainVotes)
            {
                domainTotal += kv.Value;
                if (kv.Value > bestDomainScore)
                {
                    bestDomainScore = kv.Value;
                    bestDomain = kv.Key;
                }
            }

            if (bestDomainScore >= 0.0)
            {
                result.InferredDomain = bestDomain;
                if (domainTotal <= 0.0) domainTotal = 1.0;
                result.DomainConfidence = Math.Min(1.0, bestDomainScore / domainTotal);
            }

            // Purposes: include any purpose >= 30% of the top purpose vote
            KeyPurpose inferred = KeyPurpose.NotSet;
            double bestPurposeScore = -1.0;
            double topPurposeScore = 0.0;

            // find top score
            foreach (var kv in purposeVotes)
            {
                if (kv.Value > topPurposeScore) topPurposeScore = kv.Value;
            }
            double threshold = topPurposeScore * 0.3;

            // collect flagged purposes
            foreach (var kv in purposeVotes)
            {
                if (kv.Value >= threshold)
                {
                    inferred |= kv.Key;
                }
            }

            if (inferred == KeyPurpose.NotSet)
            {
                // fallback to max
                double maxVal = -1.0;
                KeyPurpose maxKey = KeyPurpose.NotSet;
                foreach (var kv in purposeVotes)
                {
                    if (kv.Value > maxVal)
                    {
                        maxVal = kv.Value;
                        maxKey = kv.Key;
                    }
                }
                inferred = maxKey;
            }

            result.InferredPurpose = inferred;
        }

        // ---------------------------
        // FK discovery (no LINQ)
        // ---------------------------
        private void TestForeignKeyHypotheses(IControlledVocabulary source, SemanticInferenceResult result)
        {
            // Collect FK-like fields (ordered manually by confidence)
            var candidates = new System.Collections.Generic.List<FieldInferenceInfo>();
            var i = 0;
            var fields = result.FieldInferences.ToArray();
            int n = fields.Count();
            while (i < n)
            {
                var f = fields[i];
                if (f.IsPotentialForeignKey && f.ForeignKeyConfidence > 0.3)
                    candidates.Add(f);
                i++;
            }

            // Try domain-compatible vocabs first, else all
            System.Collections.Generic.List<IControlledVocabulary> targets;
            if (_registry != null)
            {
                targets = new System.Collections.Generic.List<IControlledVocabulary>();
                var byDom = _registry.GetByDomain(result.InferredDomain);
                foreach (var v in byDom) targets.Add(v);
                if (targets.Count == 0)
                {
                    // fallback to all
                    targets.Clear();
                    foreach (var v in _registry.GetAll()) targets.Add(v);
                }
            }
            else
            {
                return;
            }

            // Evaluate
            var c = 0;
            var cCount = candidates.Count;
            while (c < cCount)
            {
                var fkField = candidates[c];
                TestFieldAsForeignKey(source, fkField, targets, result);
                c++;
            }
        }

        private void TestFieldAsForeignKey(
            IControlledVocabulary source,
            FieldInferenceInfo fkField,
            System.Collections.Generic.List<IControlledVocabulary> targets,
            SemanticInferenceResult result)
        {
            var sourceField = fkField.FieldName;

            // For each target vocab, try to find best matching target field by code overlap
            var t = 0;
            var tCount = targets.Count;
            while (t < tCount)
            {
                var target = targets[t];
                if (!ReferenceEquals(target, source))
                {
                    var test = TestForeignKeyMatch(source, sourceField, target);
                    if (test.IsViable)
                    {
                        var fk = new ForeignKeyMatchResult
                        {
                            SourceField = sourceField,
                            SourceVocabulary = source.VocabularyName,
                            TargetVocabulary = target.VocabularyName,
                            TargetField = test.BestTargetField,
                            Score = (int)(test.MatchRatio * 100),
                            StrategyUsed = MatchStrategy.Exact,
                            Justification = GenerateFKReasoning(fkField, test, target),
                            MatchCount = test.MatchCount,
                            Confidence = test.MatchRatio * fkField.ForeignKeyConfidence
                        };
                        result.AddForeignKeyCandidate(fk);
                    }
                }
                t++;
            }
        }

        private ForeignKeyTestResult TestForeignKeyMatch(IControlledVocabulary source, string sourceField, IControlledVocabulary target)
        {
            var r = new ForeignKeyTestResult();

            // Normalize source values as codes (comparison-friendly)
            var srcVals = FieldFilter.ExtractFieldValuesNormalized(
                source.Records, sourceField, FieldKind.Code, false,
                LocalSettings.DefaultMaxSamples, LocalSettings.DefaultDeduplicate);

            if (srcVals.Count == 0) return r;

            // Use a HashSet for source to speed membership checks
            var srcSet = new System.Collections.Generic.HashSet<string>();
            {
                int i = 0; int n = srcVals.Count;
                while (i < n) { srcSet.Add(srcVals[i]); i++; }
            }

            // Iterate target fields (prioritize code-like fields)
            string bestField = "";
            double bestRatio = 0.0;
            int bestMatches = 0;

            var fields = target.FieldNames;
            foreach (var tf in fields)
            {
                // Collect target values normalized as codes
                var tgtVals = FieldFilter.ExtractFieldValuesNormalized(
                    target.Records, tf, FieldKind.Code, false,
                    LocalSettings.DefaultMaxSamples, LocalSettings.DefaultDeduplicate);

                if (tgtVals.Count == 0) continue;

                // Count overlaps
                int matches = 0;
                int i = 0; int m = tgtVals.Count;
                while (i < m)
                {
                    if (srcSet.Contains(tgtVals[i])) matches++;
                    i++;
                }

                if (matches == 0) continue;

                // Ratio relative to number of unique source values
                double ratio = (double)matches / (double)srcSet.Count;
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    bestField = tf;
                    bestMatches = matches;
                }
            }

            r.BestTargetField = bestField;
            r.MatchRatio = bestRatio;
            r.MatchCount = bestMatches;
            return r;
        }

        // ---------------------------
        // Small helpers (no LINQ)
        // ---------------------------
        private void InferFieldSemanticsFromName(string fieldName, FieldInferenceInfo info)
        {
            var ln = fieldName.ToLowerInvariant();

            if (Contains(ln, "taxonomy") || Contains(ln, "taxon"))
                info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.8, "Taxonomic field.");

            if (Contains(ln, "depth") || Contains(ln, "habitat"))
                info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.6, "Habitat context.");
        }

        private void AnalyzeContextField(string lname, System.Collections.Generic.List<string> samples, FieldInferenceInfo info)
        {
            if (Contains(lname, "region") || Contains(lname, "area"))
            {
                info.AddSemanticHint(KeyDomain.Geographic, KeyPurpose.SpatialExtent, 0.8, "Geographic context.");
            }
            else if (Contains(lname, "year") || Contains(lname, "date"))
            {
                info.AddSemanticHint(KeyDomain.Temporal, KeyPurpose.TimeStamp, 0.8, "Temporal context.");
            }
        }

        private double CalculateCodeFieldFKConfidence(System.Collections.Generic.List<string> samples, KeyFieldDescriptor d)
        {
            double conf = 0.5;
            if (d.AvgLength <= 5 && d.UniquenessRatio > 0.8) conf += 0.3;
            if (d.UniquenessRatio > 0.95) conf += 0.2;
            return conf > 1.0 ? 1.0 : conf;
        }

        private HierarchicalNestingAnalysis CalculateHierarchicalNesting(System.Collections.Generic.List<string> values)
        {
            var analysis = new HierarchicalNestingAnalysis();
            var separators = new char[] { '.', '-', '_', ':', '/', '\\' };

            // counts
            var sepCounts = new System.Collections.Generic.Dictionary<char, int>();
            var maxDepth = 1;
            int total = 0;

            int take = values.Count;
            if (LocalSettings.DefaultMaxSamples > 0 && LocalSettings.DefaultMaxSamples < take) take = LocalSettings.DefaultMaxSamples;

            int i = 0;
            while (i < take)
            {
                var v = values[i];
                if (!string.IsNullOrWhiteSpace(v))
                {
                    int si = 0;
                    while (si < separators.Length)
                    {
                        char sep = separators[si];
                        if (IndexOfChar(v, sep) >= 0)
                        {
                            int c = CountChar(v, sep);
                            int prev;
                            if (!sepCounts.TryGetValue(sep, out prev)) prev = 0;
                            sepCounts[sep] = prev + 1;

                            int depth = c + 1;
                            if (depth > maxDepth) maxDepth = depth;
                        }
                        si++;
                    }
                    total++;
                }
                i++;
            }

            // Choose common separators (>30% of considered values)
            var chosen = new System.Collections.Generic.List<char>();
            if (total > 0)
            {
                double threshold = total * 0.3;
                foreach (var kv in sepCounts)
                {
                    if (kv.Value > threshold) chosen.Add(kv.Key);
                }
            }

            analysis.Separators = chosen;
            analysis.MaxDepth = maxDepth;
            analysis.IsHierarchical = chosen.Count > 0 && maxDepth > 1;

            if (analysis.IsHierarchical && total > 0)
            {
                // Consistency ratio = (sum counts for chosen seps) / total
                int sum = 0;
                int j = 0;
                while (j < chosen.Count)
                {
                    int cnt;
                    if (sepCounts.TryGetValue(chosen[j], out cnt)) sum += cnt;
                    j++;
                }
                analysis.ConsistencyRatio = (double)sum / (double)total;
            }
            else
            {
                analysis.ConsistencyRatio = 0.0;
            }

            return analysis;
        }

        private string GenerateFKReasoning(FieldInferenceInfo fkField, ForeignKeyTestResult fkTest, IControlledVocabulary target)
        {
            return "Field '" + fkField.FieldName + "' matches " + fkTest.MatchCount + " values (" +
                   (fkTest.MatchRatio * 100.0).ToString("F1") + "%) with '" + target.VocabularyName + "." +
                   fkTest.BestTargetField + "' (confidence: " + fkField.ForeignKeyConfidence.ToString("F2") + ").";
        }

        // string helpers
        private static bool Contains(string s, string sub) => s.IndexOf(sub, StringComparison.Ordinal) >= 0;
        private static int IndexOfChar(string s, char c) => s.IndexOf(c);
        private static int IndexOfChar(System.Collections.Generic.List<char> list, char c)
        {
            int i = 0; int n = list.Count;
            while (i < n) { if (list[i] == c) return i; i++; }
            return -1;
        }
        private static int CountChar(string s, char c)
        {
            int count = 0;
            int i = 0; int n = s.Length;
            while (i < n) { if (s[i] == c) count++; i++; }
            return count;
        }

        private static bool IsNameField(string lname)
        {
            return Contains(lname, "name") || Contains(lname, "label") || Contains(lname, "title") ||
                   Contains(lname, "scientific") || Contains(lname, "common") || Contains(lname, "vernacular");
        }

        private static bool IsCodeField(string lname)
        {
            return Contains(lname, "code") || Contains(lname, "id") || EndsWith(lname, "_id") ||
                   lname == "alpha3" || Contains(lname, "identifier");
        }

        private static bool IsDescriptionField(string lname)
        {
            return Contains(lname, "description") || Contains(lname, "comment") ||
                   Contains(lname, "note") || Contains(lname, "remark") || Contains(lname, "detail");
        }

        private static bool IsContextField(string lname)
        {
            if (Contains(lname, "region") || Contains(lname, "area") ||
                Contains(lname, "location") || Contains(lname, "geographic") ||
                Contains(lname, "latitude") || Contains(lname, "longitude"))
                return true;

            if (Contains(lname, "year") || Contains(lname, "date") ||
                Contains(lname, "time") || Contains(lname, "period"))
                return true;

            if (Contains(lname, "version") || Contains(lname, "created") ||
                Contains(lname, "modified") || Contains(lname, "updated"))
                return true;

            return false;
        }

        private static bool EndsWith(string s, string suffix)
        {
            if (suffix.Length > s.Length) return false;
            int offset = s.Length - suffix.Length;
            int i = 0;
            while (i < suffix.Length)
            {
                if (s[offset + i] != suffix[i]) return false;
                i++;
            }
            return true;
        }

        private struct ForeignKeyTestResult
        {
            public int MatchCount;
            public double MatchRatio;
            public string BestTargetField;
            public bool IsViable { get { return MatchRatio > 0.1; } }
        }
    }
}
