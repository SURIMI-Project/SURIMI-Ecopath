public class StartsWithFieldMatcher : IFieldMatcher
{
    public double Score(string a, string b)
    {
        return b.StartsWith(a, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
    }
}