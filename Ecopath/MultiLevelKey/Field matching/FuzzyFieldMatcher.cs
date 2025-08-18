using Ecopath.Utilities;

public class FuzzyFieldMatcher : IFieldMatcher
{
    public double Score(string a, string b)
    {
        return FuzzyHelpers.FuzzyRatio(a, b);
    }
}