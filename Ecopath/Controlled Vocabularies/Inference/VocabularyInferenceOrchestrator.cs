using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Vocabulary
{
    /// <summary>
    /// Runs registered IVocabularyInferenceStrategy instances and merges their results,
    /// then infers primary Domain/Purpose from accumulated hints.
    /// </summary>
    public sealed class VocabularyInferenceOrchestrator
    {
        private readonly List<IVocabularyInferenceStrategy> _strategies = new List<IVocabularyInferenceStrategy>();

        public void RegisterStrategy(IVocabularyInferenceStrategy strategy)
        {
            if (strategy == null) return;
            _strategies.Add(strategy);
        }

        public SemanticInferenceResult Analyze(IControlledVocabulary vocabulary, ModelContext? context, IVocabularyRegistry? registry)
        {
            var acc = new SemanticInferenceResult(vocabulary.VocabularyName);

            // Run strategies in simple priority order (highest first)
            // (No LINQ: selection sort-ish pass)
            var ordered = OrderByPriorityDescending(_strategies);

            var i = 0;
            while (i < ordered.Count)
            {
                var s = ordered[i];
                var part = s.Analyze(vocabulary, context, registry);
                Apply(part, acc);
                i++;
            }

            // Synthesize primary semantics (domain & purpose) from accumulated hints
            FinalizePrimarySemantics(acc);

            return acc;
        }

        private List<IVocabularyInferenceStrategy> OrderByPriorityDescending(List<IVocabularyInferenceStrategy> src)
        {
            var list = new List<IVocabularyInferenceStrategy>();
            var i = 0;
            while (i < src.Count)
            {
                list.Add(src[i]);
                i++;
            }

            // simple insertion sort by Priority desc (stable enough for short lists)
            var j = 1;
            while (j < list.Count)
            {
                var key = list[j];
                var k = j - 1;
                while (k >= 0 && list[k].Priority < key.Priority)
                {
                    list[k + 1] = list[k];
                    k--;
                }
                list[k + 1] = key;
                j++;
            }
            return list;
        }

        private void Apply(VocabularyStrategyResult part, SemanticInferenceResult acc)
        {
            if (part == null) return;

            // Merge hints
            var i = 0;
            while (i < part.DomainHints.Count)
            {
                var h = part.DomainHints[i];
                acc.AddDomainHint(h.Domain, h.Confidence, h.Reason);
                i++;
            }

            i = 0;
            while (i < part.PurposeHints.Count)
            {
                var h = part.PurposeHints[i];
                acc.AddPurposeHint(h.Purpose, h.Confidence, h.Reason);
                i++;
            }

            // Merge FK candidates
            i = 0;
            while (i < part.ForeignKeyCandidates.Count)
            {
                acc.AddForeignKeyCandidate(part.ForeignKeyCandidates[i]);
                i++;
            }

            // Merge diagnostics
            i = 0;
            while (i < part.Diagnostics.Count)
            {
                acc.AddDiagnostic(part.Diagnostics[i]);
                i++;
            }
        }

        private void FinalizePrimarySemantics(SemanticInferenceResult acc)
        {
            // Tally domains
            var domainScores = new Dictionary<KeyDomain, double>();
            var dh = acc.DomainHints.GetEnumerator();
            while (dh.MoveNext())
            {
                var d = dh.Current;
                if (!domainScores.ContainsKey(d.Domain)) domainScores[d.Domain] = 0.0;
                domainScores[d.Domain] += d.Confidence;
            }

            // Pick best domain
            double bestScore = -1.0;
            var bestDomain = KeyDomain.NotSet;
            var dkeys = domainScores.Keys.GetEnumerator();
            while (dkeys.MoveNext())
            {
                var dom = dkeys.Current;
                var score = domainScores[dom];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDomain = dom;
                }
            }
            acc.InferredDomain = bestDomain;
            acc.DomainConfidence = bestScore > 0.0 ? Math.Min(1.0, bestScore) : 0.0;

            // Tally purposes
            var purposeScores = new Dictionary<KeyPurpose, double>();
            var ph = acc.PurposeHints.GetEnumerator();
            while (ph.MoveNext())
            {
                var p = ph.Current;
                if (!purposeScores.ContainsKey(p.Purpose)) purposeScores[p.Purpose] = 0.0;
                purposeScores[p.Purpose] += p.Confidence;
            }

            // Select all purposes with >= 30% of top score (bitflags)
            double topPurposeScore = -1.0;
            var pkeys = purposeScores.Keys.GetEnumerator();
            while (pkeys.MoveNext())
            {
                var purpose = pkeys.Current;
                var score = purposeScores[purpose];
                if (score > topPurposeScore) topPurposeScore = score;
            }

            var inferred = KeyPurpose.NotSet;
            if (topPurposeScore > 0.0)
            {
                var threshold = topPurposeScore * 0.30;
                var pkeys2 = purposeScores.Keys.GetEnumerator();
                while (pkeys2.MoveNext())
                {
                    var purpose = pkeys2.Current;
                    var score = purposeScores[purpose];
                    if (score >= threshold)
                    {
                        inferred |= purpose;
                    }
                }
            }
            else
            {
                // nothing tallied, keep NotSet
            }

            acc.InferredPurpose = inferred == KeyPurpose.NotSet && purposeScores.Count > 0
                ? BestSinglePurpose(purposeScores)
                : inferred;
        }

        private KeyPurpose BestSinglePurpose(Dictionary<KeyPurpose, double> map)
        {
            double best = -1.0;
            var bestKey = KeyPurpose.NotSet;
            var it = map.Keys.GetEnumerator();
            while (it.MoveNext())
            {
                var k = it.Current;
                var v = map[k];
                if (v > best)
                {
                    best = v;
                    bestKey = k;
                }
            }
            return bestKey;
        }
    }
}
