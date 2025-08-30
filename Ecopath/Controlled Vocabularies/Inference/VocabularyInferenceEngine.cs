using ControlledVocabularies.Common;
using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Inference.Field;
using ControlledVocabularies.Inference.Vocabulary.Strategies;
using ControlledVocabularies.Match;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Vocabulary
{
    /// <summary>
    /// Vocabulary-level inferrer/engine.
    /// - Reuses FieldFilter for sampling
    /// - Reuses KeyFieldDescriptorIndexer for descriptor stats
    /// - Delegates vocabulary-wide logic to VocabularyInferenceOrchestrator
    /// </summary>
    public sealed class VocabularyInferenceEngine
    {
        private readonly IVocabularyRegistry? _registry;
        private readonly KeyFieldDescriptorIndexer _fieldIndexer;
        private readonly VocabularyInferenceOrchestrator _orchestrator;

        // Token matching (normalized) goes through the matcher for API consistency
        private readonly ContainsFieldMatcher _contains = new ContainsFieldMatcher();

        // Purpose consensus knob; keep local to avoid forcing a LocalSettings change tonight
        // (feel free to move to LocalSettings later as PurposeInclusionFraction).
        private const double PurposeInclusionFraction = 0.30; // include any purpose >= 30% of the top vote

        // Pre-normalized token snippets used across helpers
        private static readonly string TokName = FieldPolicy.ForSchema("name");
        private static readonly string TokLabel = FieldPolicy.ForSchema("label");
        private static readonly string TokTitle = FieldPolicy.ForSchema("title");
        private static readonly string TokScientific = FieldPolicy.ForSchema("scientific");
        private static readonly string TokCommon = FieldPolicy.ForSchema("common");
        private static readonly string TokVernacular = FieldPolicy.ForSchema("vernacular");
        private static readonly string TokGear = FieldPolicy.ForSchema("gear");
        private static readonly string TokCode = FieldPolicy.ForSchema("code");
        private static readonly string TokId = FieldPolicy.ForSchema("id");
        private static readonly string TokIdentifier = FieldPolicy.ForSchema("identifier");
        private static readonly string TokAlpha3 = FieldPolicy.ForSchema("alpha3");
        private static readonly string TokDescription = FieldPolicy.ForSchema("description");
        private static readonly string TokComment = FieldPolicy.ForSchema("comment");
        private static readonly string TokNote = FieldPolicy.ForSchema("note");
        private static readonly string TokRemark = FieldPolicy.ForSchema("remark");
        private static readonly string TokDetail = FieldPolicy.ForSchema("detail");
        private static readonly string TokRegion = FieldPolicy.ForSchema("region");
        private static readonly string TokArea = FieldPolicy.ForSchema("area");
        private static readonly string TokLocation = FieldPolicy.ForSchema("location");
        private static readonly string TokGeographic = FieldPolicy.ForSchema("geographic");
        private static readonly string TokLatitude = FieldPolicy.ForSchema("latitude");
        private static readonly string TokLongitude = FieldPolicy.ForSchema("longitude");
        private static readonly string TokYear = FieldPolicy.ForSchema("year");
        private static readonly string TokDate = FieldPolicy.ForSchema("date");
        private static readonly string TokTime = FieldPolicy.ForSchema("time");
        private static readonly string TokPeriod = FieldPolicy.ForSchema("period");
        private static readonly string TokVersion = FieldPolicy.ForSchema("version");
        private static readonly string TokCreated = FieldPolicy.ForSchema("created");
        private static readonly string TokModified = FieldPolicy.ForSchema("modified");
        private static readonly string TokUpdated = FieldPolicy.ForSchema("updated");
        private static readonly string TokBinomial = FieldPolicy.ForSchema("binomial");
        private static readonly string TokSpecies = FieldPolicy.ForSchema("species");
        private static readonly string TokTaxonomy = FieldPolicy.ForSchema("taxonomy");
        private static readonly string TokTaxon = FieldPolicy.ForSchema("taxon");
        private static readonly string TokHabitat = FieldPolicy.ForSchema("habitat");
        private static readonly string TokDepth = FieldPolicy.ForSchema("depth");

        public VocabularyInferenceEngine(IVocabularyRegistry? registry = null)
        {
            _registry = registry;
            _fieldIndexer = new KeyFieldDescriptorIndexer(registry);

            _orchestrator = new VocabularyInferenceOrchestrator();
            _orchestrator.RegisterStrategy(new NamePatternVocabularyStrategy());
            _orchestrator.RegisterStrategy(new DescriptorConsensusStrategy());
            _orchestrator.RegisterStrategy(new ForeignKeyDiscoveryStrategy());
        }

        /// <summary>
        /// Analyze a controlled vocabulary to infer its intended semantics (domain, purpose, FK candidates).
        /// </summary>
        public SemanticInferenceResult Analyze(IControlledVocabulary vocabulary)
        {
            var context = GlobalServiceLocator.Get<ModelContext>();
            var result = new SemanticInferenceResult(vocabulary.VocabularyName);

            // 1) Field-level analysis
            var fieldsEnumerator = vocabulary.FieldNames.GetEnumerator();
            while (fieldsEnumerator.MoveNext())
            {
                var fieldName = fieldsEnumerator.Current;
                var fieldInfo = AnalyzeField(vocabulary, fieldName);
                result.AddFieldInference(fieldInfo);
            }

            // 2) Vocabulary-wide strategies (composed)
            var composite = _orchestrator.Analyze(vocabulary, context, _registry);

            // Merge strategy outputs (keep generic reason to avoid coupling on strategy payload shape)
            var dhEnum = composite.DomainHints.GetEnumerator();
            while (dhEnum.MoveNext())
            {
                result.AddDomainHint(dhEnum.Current.Domain, dhEnum.Current.Confidence, "Strategy votes");
            }

            var phEnum = composite.PurposeHints.GetEnumerator();
            while (phEnum.MoveNext())
            {
                result.AddPurposeHint(phEnum.Current.Purpose, phEnum.Current.Confidence, "Strategy votes");
            }

            var fkEnum = composite.ForeignKeyCandidates.GetEnumerator();
            while (fkEnum.MoveNext())
            {
                result.AddForeignKeyCandidate(fkEnum.Current);
            }

            var diEnum = composite.Diagnostics.GetEnumerator();
            while (diEnum.MoveNext())
            {
                result.AddDiagnostic(diEnum.Current);
            }

            // 3) Consensus on primary semantics
            InferPrimarySemantics(result);

            return result;
        }

        // ---------------------------
        // Field analysis
        // ---------------------------
        private FieldInferenceInfo AnalyzeField(IControlledVocabulary vocab, string fieldName)
        {
            var info = new FieldInferenceInfo(fieldName);

            // Sample values
            var samples = FieldFilter.ExtractFieldValues(vocab.Records, fieldName);
            if (samples.Count == 0)
            {
                info.AddDiagnostic("No sample values for field.");
                return info;
            }

            // Descriptor (ensure indexed once)
            var descriptor = GetOrCreateDescriptor(vocab, fieldName, samples);
            info.SetDescriptor(descriptor);

            // Importance from name signals
            AnalyzeImportance(fieldName, samples, descriptor, info);

            // Code specifics: hierarchical patterns (taxonomic-like dot trees, etc.)
            if (descriptor.Kind == FieldKind.Code)
            {
                var nesting = CalculateHierarchicalNesting(samples);
                info.SetHierarchicalNesting(nesting);

                if (nesting.IsHierarchical)
                {
                    info.AddReason("Hierarchical code structure detected.");
                    if (IndexOfChar(nesting.Separators, '.') >= 0)
                    {
                        info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.6, "Dot-separated hierarchy – taxonomic hint.");
                    }
                }
            }

            // Strategy hints (check Exact+Fuzzy via mask)
            const MatchStrategy NameMask = MatchStrategy.Exact | MatchStrategy.Fuzzy;
            if ((descriptor.Strategy & NameMask) == NameMask)
            {
                info.AddReason("Exact+Fuzzy strategy – likely name/label.");
            }
            else if (descriptor.Strategy == MatchStrategy.Exact && descriptor.Kind == FieldKind.Code)
            {
                info.IsPotentialForeignKey = true;
                if (info.ForeignKeyConfidence < 0.7) info.ForeignKeyConfidence = 0.7;
                info.AddReason("Exact-only code – likely identifier/foreign key.");
            }

            // Quick semantic nudges from field name tokens
            InferFieldSemanticsFromName(fieldName, info);

            return info;
        }

        private KeyFieldDescriptor GetOrCreateDescriptor(IControlledVocabulary vocab, string fieldName, List<string> samples)
        {
            var descriptor = vocab.GetKeyFieldDescriptor(fieldName);
            if (descriptor == null)
            {
                descriptor = new KeyFieldDescriptor(fieldName, KeyDomain.NotSet, KeyPurpose.NotSet, FieldKind.Unknown, false, 0, MatchStrategy.None);
            }

            // Avoid re-indexing when already done
            if (!descriptor.IsIndexed)
            {
                _fieldIndexer.BuildIndex(fieldName, vocab.Records, descriptor);
            }

            return descriptor;
        }

        private void AnalyzeImportance(string fieldName, List<string> samples, KeyFieldDescriptor descriptor, FieldInferenceInfo info)
        {
            // Normalize the field name once using policy, then test via ContainsFieldMatcher
            var lname = FieldPolicy.ForSchema(fieldName);

            if (IsNameField(lname))
            {
                info.ImportanceWeight = FieldImportanceWeight.Name;
                info.AddReason("Name field – high semantic importance.");

                if (ContainsToken(lname, TokScientific) || ContainsToken(lname, TokBinomial) || ContainsToken(lname, TokSpecies))
                {
                    info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.95, "Scientific name.");
                }
                else if (ContainsToken(lname, TokCommon) || ContainsToken(lname, TokVernacular))
                {
                    info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.85, "Common name.");
                }
                else if (ContainsToken(lname, TokGear) && ContainsToken(lname, TokName))
                {
                    info.AddSemanticHint(KeyDomain.FleetSegment, KeyPurpose.Gear, 0.9, "Gear name.");
                }
                return;
            }

            if (IsCodeField(lname) || descriptor.Kind == FieldKind.Code)
            {
                info.ImportanceWeight = FieldImportanceWeight.Code;
                info.IsPotentialForeignKey = true;
                info.ForeignKeyConfidence = CalculateCodeFieldFKConfidence(samples, descriptor);
                info.AddReason("Code field – potential PK or FK.");
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
        // Consensus (no LINQ)
        // ---------------------------
        private void InferPrimarySemantics(SemanticInferenceResult result)
        {
            var domainVotes = new Dictionary<KeyDomain, double>();
            var purposeVotes = new Dictionary<KeyPurpose, double>();

            // Field-driven votes (weighted by importance)
            var fields = result.FieldInferences.GetEnumerator();
            while (fields.MoveNext())
            {
                var field = fields.Current;
                double importance = (int)field.ImportanceWeight / 5.0;

                var hints = field.SemanticHints;
                int i = 0;
                int n = hints.Count;
                while (i < n)
                {
                    var hint = hints[i];
                    double weighted = hint.Confidence * importance;

                    double prevD;
                    if (!domainVotes.TryGetValue(hint.Domain, out prevD)) prevD = 0.0;
                    domainVotes[hint.Domain] = prevD + weighted;

                    double prevP;
                    if (!purposeVotes.TryGetValue(hint.Purpose, out prevP)) prevP = 0.0;
                    purposeVotes[hint.Purpose] = prevP + weighted;

                    i++;
                }
            }

            // Merge vocabulary-level hints (from strategies)
            var dh = result.DomainHints.GetEnumerator();
            while (dh.MoveNext())
            {
                double prev;
                if (!domainVotes.TryGetValue(dh.Current.Domain, out prev)) prev = 0.0;
                domainVotes[dh.Current.Domain] = prev + dh.Current.Confidence;
            }

            var ph = result.PurposeHints.GetEnumerator();
            while (ph.MoveNext())
            {
                double prev;
                if (!purposeVotes.TryGetValue(ph.Current.Purpose, out prev)) prev = 0.0;
                purposeVotes[ph.Current.Purpose] = prev + ph.Current.Confidence;
            }

            // Pick best domain + confidence (score / sum)
            KeyDomain bestDomain = KeyDomain.NotSet;
            double bestScore = -1.0;
            double sumScores = 0.0;

            var dvEnum = domainVotes.GetEnumerator();
            while (dvEnum.MoveNext())
            {
                var kv = dvEnum.Current;
                sumScores += kv.Value;
                if (kv.Value > bestScore)
                {
                    bestScore = kv.Value;
                    bestDomain = kv.Key;
                }
            }

            if (bestScore >= 0.0)
            {
                result.InferredDomain = bestDomain;
                if (sumScores <= 0.0) sumScores = 1.0;
                result.DomainConfidence = System.Math.Min(1.0, bestScore / sumScores);
            }

            // Purposes: include any >= fraction of the top purpose score
            double topPurpose = 0.0;
            var pvEnum1 = purposeVotes.GetEnumerator();
            while (pvEnum1.MoveNext())
            {
                if (pvEnum1.Current.Value > topPurpose) topPurpose = pvEnum1.Current.Value;
            }

            KeyPurpose inferred = KeyPurpose.NotSet;
            double threshold = topPurpose * PurposeInclusionFraction;

            var pvEnum2 = purposeVotes.GetEnumerator();
            while (pvEnum2.MoveNext())
            {
                if (pvEnum2.Current.Value >= threshold)
                {
                    inferred |= pvEnum2.Current.Key;
                }
            }

            if (inferred == KeyPurpose.NotSet)
            {
                // fallback: choose the max
                double maxVal = -1.0;
                KeyPurpose maxKey = KeyPurpose.NotSet;
                var pvEnum3 = purposeVotes.GetEnumerator();
                while (pvEnum3.MoveNext())
                {
                    if (pvEnum3.Current.Value > maxVal)
                    {
                        maxVal = pvEnum3.Current.Value;
                        maxKey = pvEnum3.Current.Key;
                    }
                }
                inferred = maxKey;
            }

            result.InferredPurpose = inferred;
        }

        // ---------------------------
        // Small helpers (no LINQ)
        // ---------------------------
        private void InferFieldSemanticsFromName(string fieldName, FieldInferenceInfo info)
        {
            var lname = FieldPolicy.ForSchema(fieldName);

            if (ContainsToken(lname, TokTaxonomy) || ContainsToken(lname, TokTaxon))
            {
                info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.8, "Taxonomic field.");
            }

            if (ContainsToken(lname, TokDepth) || ContainsToken(lname, TokHabitat))
            {
                info.AddSemanticHint(KeyDomain.Species, KeyPurpose.Species, 0.6, "Habitat context.");
            }
        }

        private void AnalyzeContextField(string lname, List<string> samples, FieldInferenceInfo info)
        {
            if (ContainsToken(lname, TokRegion) || ContainsToken(lname, TokArea))
            {
                info.AddSemanticHint(KeyDomain.Geographic, KeyPurpose.SpatialExtent, 0.8, "Geographic context.");
            }
            else if (ContainsToken(lname, TokYear) || ContainsToken(lname, TokDate))
            {
                info.AddSemanticHint(KeyDomain.Temporal, KeyPurpose.TimeStamp, 0.8, "Temporal context.");
            }
        }

        private double CalculateCodeFieldFKConfidence(List<string> samples, KeyFieldDescriptor d)
        {
            double conf = 0.5;
            if (d.AvgLength <= 5 && d.UniquenessRatio > 0.8) conf += 0.3;
            if (d.UniquenessRatio > 0.95) conf += 0.2;
            return conf > 1.0 ? 1.0 : conf;
        }

        private HierarchicalNestingAnalysis CalculateHierarchicalNesting(List<string> values)
        {
            var analysis = new HierarchicalNestingAnalysis();
            var separators = new char[] { '.', '-', '_', ':', '/', '\\' };

            var sepCounts = new Dictionary<char, int>();
            var maxDepth = 1;
            int total = 0;

            int take = values.Count;
            if (LocalSettings.DefaultMaxSamples > 0 && LocalSettings.DefaultMaxSamples < take)
                take = LocalSettings.DefaultMaxSamples;

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

            var chosen = new List<char>();
            if (total > 0)
            {
                double threshold = total * 0.3; // 30% usage threshold
                var it = sepCounts.GetEnumerator();
                while (it.MoveNext())
                {
                    if (it.Current.Value > threshold) chosen.Add(it.Current.Key);
                }
            }

            analysis.Separators = chosen;
            analysis.MaxDepth = maxDepth;
            analysis.IsHierarchical = chosen.Count > 0 && maxDepth > 1;

            if (analysis.IsHierarchical && total > 0)
            {
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

        // Normalized token containment via matcher
        private bool ContainsToken(string normalizedHaystack, string normalizedNeedle)
        {
            return _contains.Score(normalizedNeedle, normalizedHaystack) > 0.0;
        }

        // Field kind/name heuristics (normalized names)
        private bool IsNameField(string lname)
        {
            return ContainsToken(lname, TokName) ||
                    ContainsToken(lname, TokLabel) ||
                    ContainsToken(lname, TokTitle) ||
                    ContainsToken(lname, TokScientific) ||
                    ContainsToken(lname, TokCommon) ||
                    ContainsToken(lname, TokVernacular);
        }

        private bool IsCodeField(string lname)
        {
            // suffix heuristic kept literal (schema-normalized "key" remains "key")
            if (EndsWith(lname, "key")) return true;

            return ContainsToken(lname, TokCode) ||
                    ContainsToken(lname, TokId) ||
                    lname == TokAlpha3 ||
                    ContainsToken(lname, TokIdentifier);
        }

        private bool IsDescriptionField(string lname)
        {
            return ContainsToken(lname, TokDescription) ||
                    ContainsToken(lname, TokComment) ||
                    ContainsToken(lname, TokNote) ||
                    ContainsToken(lname, TokRemark) ||
                    ContainsToken(lname, TokDetail);
        }

        private bool IsContextField(string lname)
        {
            if (ContainsToken(lname, TokRegion) || ContainsToken(lname, TokArea) ||
                ContainsToken(lname, TokLocation) || ContainsToken(lname, TokGeographic) ||
                ContainsToken(lname, TokLatitude) || ContainsToken(lname, TokLongitude))
                return true;

            if (ContainsToken(lname, TokYear) || ContainsToken(lname, TokDate) ||
                ContainsToken(lname, TokTime) || ContainsToken(lname, TokPeriod))
                return true;

            if (ContainsToken(lname, TokVersion) || ContainsToken(lname, TokCreated) ||
                ContainsToken(lname, TokModified) || ContainsToken(lname, TokUpdated))
                return true;

            return false;
        }

        // Small string helpers
        private static int IndexOfChar(string s, char c) => s.IndexOf(c);

        private static int IndexOfChar(List<char> list, char c)
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
    }
}
