using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Strategies
{
    public sealed class ForeignKeyByOverlapStrategy : IVocabularyAnalysisStrategy
    {
        private readonly IVocabularyRegistry? _registry;

        public ForeignKeyByOverlapStrategy(IVocabularyRegistry? registry = null)
        {
            _registry = registry;
        }

        public string Name => "ForeignKeyByOverlap";
        public double Priority => 7.5;

        public void Analyze(IControlledVocabulary source, ModelContext context, SemanticInferenceResult acc, System.Func<string, FieldInferenceInfo> fieldInfoProvider)
        {
            if (_registry == null)
            {
                acc.AddDiagnostic("Registry unavailable - FK overlap strategy skipped");
                return;
            }

            // Collect candidate FK fields
            var candidates = new List<FieldInferenceInfo>();
            var fEnum = source.FieldNames.GetEnumerator();
            while (fEnum.MoveNext())
            {
                var fName = fEnum.Current;
                var info = fieldInfoProvider(fName);
                if (info == null) continue;

                if (info.IsPotentialForeignKey && info.ForeignKeyConfidence > 0.30)
                {
                    candidates.Add(info);
                }
            }

            if (candidates.Count == 0) return;

            // Choose target set: prefer same domain as source (if set), else all
            var targets = new List<IControlledVocabulary>();
            var preferred = _registry.GetByDomain(source.Domain).GetEnumerator();
            while (preferred.MoveNext())
            {
                var t = preferred.Current;
                if (!object.ReferenceEquals(t, source))
                {
                    targets.Add(t);
                }
            }
            if (targets.Count == 0)
            {
                var all = _registry.GetAll().GetEnumerator();
                while (all.MoveNext())
                {
                    var t = all.Current;
                    if (!object.ReferenceEquals(t, source))
                    {
                        targets.Add(t);
                    }
                }
            }

            // Test each candidate against targets
            int i = 0;
            while (i < candidates.Count)
            {
                var fkField = candidates[i];
                var srcFieldName = fkField.FieldName;

                int j = 0;
                while (j < targets.Count)
                {
                    var target = targets[j];

                    var test = TestForeignKeyMatch(source, srcFieldName, target);
                    if (test.IsViable)
                    {
                        var fk = new ForeignKeyMatchResult
                        {
                            SourceField = srcFieldName,
                            SourceVocabulary = source.VocabularyName,
                            TargetVocabulary = target.VocabularyName,
                            TargetField = test.BestTargetField,
                            MatchCount = test.MatchCount,
                            Score = (int)(test.MatchRatio * 100.0),
                            Confidence = test.MatchRatio * fkField.ForeignKeyConfidence,
                            Justification = BuildReason(fkField, test, target)
                        };
                        acc.AddForeignKeyCandidate(fk);
                    }

                    j++;
                }

                i++;
            }
        }

        private string BuildReason(FieldInferenceInfo fkField, ForeignKeyTestResult test, IControlledVocabulary target)
        {
            return "Field '" + fkField.FieldName + "' matches " + test.MatchCount + " values (" +
                   (test.MatchRatio * 100.0).ToString("F1") + "%) with '" + target.VocabularyName + "." +
                   test.BestTargetField + "' (confidence: " + fkField.ForeignKeyConfidence.ToString("F2") + ")";
        }

        private sealed class ForeignKeyTestResult
        {
            public int MatchCount;
            public double MatchRatio;
            public string BestTargetField = "";
            public bool IsViable => MatchRatio > 0.10 || MatchCount >= 3;
        }

        private ForeignKeyTestResult TestForeignKeyMatch(IControlledVocabulary source, string sourceField, IControlledVocabulary target)
        {
            var result = new ForeignKeyTestResult();
            var sourceValues = CollectSamples(source, sourceField, 50, FieldKind.Code);
            if (sourceValues.Count == 0) return result;

            // Prioritize code fields at target
            var targetFields = CollectTargetFieldOrder(target);

            int i = 0;
            while (i < targetFields.Count)
            {
                var targetField = targetFields[i];
                var targetValues = CollectSamples(target, targetField, 100, FieldKind.Code);
                if (targetValues.Count > 0)
                {
                    var matches = CountIntersection(sourceValues, targetValues);
                    var ratio = (double)matches / (double)sourceValues.Count;

                    if (ratio > result.MatchRatio)
                    {
                        result.MatchRatio = ratio;
                        result.MatchCount = matches;
                        result.BestTargetField = targetField;
                    }
                }
                i++;
            }

            return result;
        }

        private System.Collections.Generic.HashSet<string> CollectSamples(IControlledVocabulary vocab, string fieldName, int max, FieldKind normalizeAs)
        {
            var set = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var seen = 0;

            var recs = vocab.Records.GetEnumerator();
            while (recs.MoveNext())
            {
                var r = recs.Current;
                var f = r.GetField(fieldName);
                if (f != null)
                {
                    var raw = f.Value;
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var norm = Utils.FieldPolicy.ForValue(raw, normalizeAs, false);
                        if (!string.IsNullOrWhiteSpace(norm))
                        {
                            set.Add(norm);
                            seen++;
                            if (seen >= max) break;
                        }
                    }
                }
            }
            return set;
        }

        private System.Collections.Generic.List<string> CollectTargetFieldOrder(IControlledVocabulary target)
        {
            var list = new System.Collections.Generic.List<string>();
            var codes = new System.Collections.Generic.List<string>();
            var others = new System.Collections.Generic.List<string>();

            var it = target.FieldNames.GetEnumerator();
            while (it.MoveNext())
            {
                var fname = it.Current;
                var d = target.GetKeyFieldDescriptor(fname);
                if (d != null && d.Kind == FieldKind.Code)
                {
                    codes.Add(fname);
                }
                else
                {
                    others.Add(fname);
                }
            }

            // Append code fields first, then others
            int i = 0;
            while (i < codes.Count) { list.Add(codes[i]); i++; }
            i = 0;
            while (i < others.Count) { list.Add(others[i]); i++; }

            return list;
        }

        private int CountIntersection(System.Collections.Generic.HashSet<string> a, System.Collections.Generic.HashSet<string> b)
        {
            int count = 0;
            var e = a.GetEnumerator();
            while (e.MoveNext())
            {
                if (b.Contains(e.Current)) count++;
            }
            return count;
        }
    }
}
