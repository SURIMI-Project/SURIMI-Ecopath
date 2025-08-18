using Ecopath.Utilities;

public class TokenOverlapFieldMatcher : IFieldMatcher
{
    public double Score(string a, string b)
    {
       return FuzzyHelpers.TokenSetFuzzyRatio(a, b);
    }
}