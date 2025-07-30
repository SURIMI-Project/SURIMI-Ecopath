using Utilities;

public class FuzzyFieldMatcher : IFieldMatcher
{
    public double Score(string field, string valueA, string valueB)
    {
        return (double)NameUtilities.TokenSetFuzzyMatch(valueA, valueB);
    }
}
