using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Vocabulary.Strategies
{
    public sealed class ForeignKeyDiscoveryStrategy : IVocabularyInferenceStrategy
    {
        private readonly IVocabularyRegistry? _injectedRegistry;

        public ForeignKeyDiscoveryStrategy(IVocabularyRegistry? registry = null)
        {
            _injectedRegistry = registry;
        }

        public string Name => "ForeignKeyDiscovery";
        public double Priority => 7.5;

        public VocabularyStrategyResult Analyze(IControlledVocabulary vocabulary, ModelContext? modelContext, IVocabularyRegistry? registry)
        {
            var result = new VocabularyStrategyResult();

            var reg = _injectedRegistry ?? registry;
            if (reg == null)
            {
                result.AddDiagnostic("Registry unavailable - FK discovery skipped.");
                return result;
            }

            // 1) Source FK candidates
            var candidates = new List<string>();
            foreach (string name in vocabulary.FieldNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var descr = vocabulary.GetKeyFieldDescriptor(name);
                if (IsLikelyForeignKeyCandidate(name, descr)) candidates.Add(name);
            }
            if (candidates.Count == 0) return result;

            // 2) Targets
            var targets = new List<IControlledVocabulary>();
            foreach (var t in reg.GetByDomain(vocabulary.Domain))
            {
                if (!object.ReferenceEquals(t, vocabulary)) targets.Add(t);
            }
            if (targets.Count == 0)
            {
                foreach (var t in reg.GetAll())
                {
                    if (!object.ReferenceEquals(t, vocabulary)) targets.Add(t);
                }
            }

            // 3) Score overlaps
            var i = 0;
            while (i < candidates.Count)
            {
                var sourceField = candidates[i];
                var srcValues = CollectNormalizedSamples(vocabulary, sourceField, FieldKind.Code);
                if (srcValues.Count == 0) { i++; continue; }

                var srcDescr = vocabulary.GetKeyFieldDescriptor(sourceField);
                var fkBaseConf = EstimateFkConfidence(srcDescr);

                var j = 0;
                while (j < targets.Count)
                {
                    var target = targets[j];
                    var test = TestForeignKeyMatch(srcValues, target);
                    if (test.IsViable)
                    {
                        var fk = new ForeignKeyMatchResult
                        {
                            SourceField = sourceField,
                            SourceVocabulary = vocabulary.VocabularyName,
                            TargetVocabulary = target.VocabularyName,
                            TargetField = test.BestTargetField,
                            MatchCount = test.MatchCount,
                            Score = (int)(test.MatchRatio * 100.0),
                            Confidence = test.MatchRatio * fkBaseConf,
                            Justification = BuildReason(sourceField, fkBaseConf, test, target)
                        };
                        result.AddForeignKeyCandidate(fk);
                    }
                    j++;
                }

                i++;
            }

            return result;
        }

        private bool IsLikelyForeignKeyCandidate(string fieldName, KeyFieldDescriptor? d)
        {
            var schema = FieldPolicy.ForSchema(fieldName);
            var looksCode =
                schema.Contains("code") ||
                schema.Contains("id") ||
                schema.EndsWith("-id") ||
                schema == "alpha3";

            if (d == null) return looksCode;
            if (d.Kind == FieldKind.Code) return true;
            if (d.AvgLength > 0 && d.AvgLength <= 6 && d.UniquenessRatio >= 0.7) return true;
            return looksCode;
        }

        private double EstimateFkConfidence(KeyFieldDescriptor? d)
        {
            double c = 0.5;
            if (d == null) return c;
            if (d.Kind == FieldKind.Code) c += 0.3;
            if (d.AvgLength > 0 && d.AvgLength <= 5) c += 0.2;
            if (d.UniquenessRatio > 0.80) c += 0.2;
            if (c > 1.0) c = 1.0;
            if (c < 0.0) c = 0.0;
            return c;
        }

        private string BuildReason(string sourceField, double fkConf, ForeignKeyTestResult test, IControlledVocabulary target)
        {
            return "Field '" + sourceField + "' matches " + test.MatchCount + " values (" +
                   (test.MatchRatio * 100.0).ToString("F1") + "%) with '" + target.VocabularyName + "." +
                   test.BestTargetField + "' (confidence: " + fkConf.ToString("F2") + ")";
        }

        private sealed class ForeignKeyTestResult
        {
            public int MatchCount;
            public double MatchRatio;
            public string BestTargetField = "";
            public bool IsViable => MatchRatio > 0.10 || MatchCount >= 3;
        }

        private ForeignKeyTestResult TestForeignKeyMatch(HashSet<string> normalizedSourceValues, IControlledVocabulary target)
        {
            var res = new ForeignKeyTestResult();
            var fields = CollectTargetFieldOrder(target);
            var k = 0;
            while (k < fields.Count)
            {
                var tf = fields[k];
                var tvals = CollectNormalizedSamples(target, tf, FieldKind.Code);
                if (tvals.Count > 0)
                {
                    var matches = CountIntersection(normalizedSourceValues, tvals);
                    var ratio = (double)matches / (double)normalizedSourceValues.Count;

                    if (ratio > res.MatchRatio)
                    {
                        res.MatchRatio = ratio;
                        res.MatchCount = matches;
                        res.BestTargetField = tf;
                    }
                }
                k++;
            }
            return res;
        }

        private HashSet<string> CollectNormalizedSamples(IControlledVocabulary vocab, string fieldName, FieldKind normalizeAs)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var values = FieldFilter.ExtractFieldValues(vocab.Records, fieldName);
            var i = 0;
            while (i < values.Count)
            {
                var raw = values[i];
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var norm = FieldPolicy.ForValue(raw, normalizeAs, false);
                    if (!string.IsNullOrWhiteSpace(norm)) set.Add(norm);
                }
                i++;
            }
            return set;
        }

        private List<string> CollectTargetFieldOrder(IControlledVocabulary target)
        {
            var list = new List<string>();
            var codes = new List<string>();
            var others = new List<string>();

            var it = target.FieldNames.GetEnumerator();
            while (it.MoveNext())
            {
                var fname = it.Current;
                var d = target.GetKeyFieldDescriptor(fname);
                if (d != null && d.Kind == FieldKind.Code) codes.Add(fname);
                else others.Add(fname);
            }

            var i = 0;
            while (i < codes.Count) { list.Add(codes[i]); i++; }
            i = 0;
            while (i < others.Count) { list.Add(others[i]); i++; }

            return list;
        }

        private int CountIntersection(HashSet<string> a, HashSet<string> b)
        {
            var count = 0;
            var e = a.GetEnumerator();
            while (e.MoveNext())
            {
                if (b.Contains(e.Current)) count++;
            }
            return count;
        }
    }
}
