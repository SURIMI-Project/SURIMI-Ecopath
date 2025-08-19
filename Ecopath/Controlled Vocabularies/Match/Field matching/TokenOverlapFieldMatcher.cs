using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Match
{
    public class TokenOverlapFieldMatcher : IFieldMatcher
    {
        public double Score(string a, string b)
        {
            return FuzzyHelpers.TokenSetFuzzyRatio(a, b);
        }
    }
}