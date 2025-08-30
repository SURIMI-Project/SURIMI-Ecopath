using ControlledVocabularies.Core;

namespace ControlledVocabularies.Inference.Vocabulary
{
    public sealed class CompositeVocabularyInferenceResult
    {
        public readonly Dictionary<KeyDomain, double> DomainVotes = new Dictionary<KeyDomain, double>();
        public readonly Dictionary<KeyPurpose, double> PurposeVotes = new Dictionary<KeyPurpose, double>();
        public readonly List<ForeignKeyMatchResult> ForeignKeys = new List<ForeignKeyMatchResult>();
        public readonly List<string> Diagnostics = new List<string>();

        public void Accumulate(VocabularyStrategyResult sr)
        {
            int i = 0;

            // domain
            var dh = sr.DomainHints;
            while (i < dh.Count)
            {
                var item = dh[i];
                double prev;
                if (!DomainVotes.TryGetValue(item.Domain, out prev)) prev = 0.0;
                DomainVotes[item.Domain] = prev + item.Confidence;
                i++;
            }

            // purpose
            i = 0;
            var ph = sr.PurposeHints;
            while (i < ph.Count)
            {
                var item = ph[i];
                double prev;
                if (!PurposeVotes.TryGetValue(item.Purpose, out prev)) prev = 0.0;
                PurposeVotes[item.Purpose] = prev + item.Confidence;
                i++;
            }

            // fk
            i = 0;
            var fk = sr.ForeignKeyCandidates;
            while (i < fk.Count)
            {
                ForeignKeys.Add(fk[i]);
                i++;
            }

            // diagnostics
            i = 0;
            var dx = sr.Diagnostics;
            while (i < dx.Count)
            {
                Diagnostics.Add(dx[i]);
                i++;
            }
        }
    }
}
