public class ContainsFieldMatcher : IFieldMatcher
{
    public double Score(string a, string b)
    {
        return b.Contains(a, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
    }
}