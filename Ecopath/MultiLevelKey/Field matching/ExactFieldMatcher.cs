public class ExactFieldMatcher : IFieldMatcher
{
    public double Score(string valueA, string valueB)
    {
        return string.Equals(valueA, valueB, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
    }
}