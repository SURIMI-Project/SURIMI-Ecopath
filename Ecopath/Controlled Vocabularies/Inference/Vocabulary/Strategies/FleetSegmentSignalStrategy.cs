using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Strategies
{
    public sealed class FleetSegmentSignalStrategy : IVocabularyAnalysisStrategy
    {
        private readonly ContainsFieldMatcher _contains = new ContainsFieldMatcher();

        public string Name => "FleetSegmentSignal";
        public double Priority => 8.0;

        public void Analyze( IControlledVocabulary vocab, ModelContext context, SemanticInferenceResult acc, System.Func<string, FieldInferenceInfo> fieldInfoProvider)
        {
            var fields = vocab.FieldNames.GetEnumerator();
            while (fields.MoveNext())
            {
                var raw = fields.Current;
                var fname = FieldPolicy.ForSchema(raw);

                // Tokens to detect
                if (_contains.Score("gear", fname) > 0.0 ||
                    _contains.Score("fleet", fname) > 0.0 ||
                    _contains.Score("metier", fname) > 0.0 ||
                    _contains.Score("vessel", fname) > 0.0 ||
                    _contains.Score("segment", fname) > 0.0)
                {
                    acc.AddDomainHint(KeyDomain.FleetSegment, 0.6, "Field name suggests fleet/metier domain");
                    acc.AddPurposeHint(KeyPurpose.Gear, 0.55, "Field suggests gear");
                    acc.AddPurposeHint(KeyPurpose.Fleet, 0.55, "Field suggests fleet");
                }

                // Also look into field reasons (if any)
                var info = fieldInfoProvider(raw);
                if (info != null && info.Reasons != null)
                {
                    int i = 0;
                    while (i < info.Reasons.Count)
                    {
                        var reason = FieldPolicy.ForSchema(info.Reasons[i]);
                        if (_contains.Score("fleet", reason) > 0.0 ||
                            _contains.Score("metier", reason) > 0.0)
                        {
                            acc.AddDomainHint(KeyDomain.FleetSegment, 0.5, "Reason text suggests fleet/metier");
                            acc.AddPurposeHint(KeyPurpose.Gear, 0.5, "Reason suggests gear");
                            acc.AddPurposeHint(KeyPurpose.Fleet, 0.5, "Reason suggests fleet");
                            break;
                        }
                        i++;
                    }
                }
            }
        }
    }
}
