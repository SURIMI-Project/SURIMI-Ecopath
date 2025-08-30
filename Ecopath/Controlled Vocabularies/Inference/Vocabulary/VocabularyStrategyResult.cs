using System.Collections.Generic;
using ControlledVocabularies.Core;

namespace ControlledVocabularies.Inference.Vocabulary
{
    public sealed class VocabularyStrategyResult
    {
        public struct DomainHint { public KeyDomain Domain; public double Confidence; public string Reason; }
        public struct PurposeHint { public KeyPurpose Purpose; public double Confidence; public string Reason; }

        public List<DomainHint> DomainHints { get; } = new();
        public List<PurposeHint> PurposeHints { get; } = new();
        public List<ForeignKeyMatchResult> ForeignKeyCandidates { get; } = new();
        public List<string> Diagnostics { get; } = new();

        public void AddDomainHint(KeyDomain d, double conf, string reason)
        {
            var h = new DomainHint { Domain = d, Confidence = conf, Reason = reason ?? "" };
            DomainHints.Add(h);
        }

        public void AddPurposeHint(KeyPurpose p, double conf, string reason)
        {
            var h = new PurposeHint { Purpose = p, Confidence = conf, Reason = reason ?? "" };
            PurposeHints.Add(h);
        }

        public void AddForeignKeyCandidate(ForeignKeyMatchResult fk) { if (fk != null) ForeignKeyCandidates.Add(fk); }
        public void AddDiagnostic(string s) { if (!string.IsNullOrWhiteSpace(s)) Diagnostics.Add(s); }
    }
}
