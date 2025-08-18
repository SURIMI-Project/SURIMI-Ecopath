using System.Text.RegularExpressions;

public class RegexFieldMatcher : IFieldMatcher
{
    private readonly string m_pattern;

    public RegexFieldMatcher(string pattern)
    {
        m_pattern = pattern;
    }

    public double Score(string a, string b)
    {
        return Regex.IsMatch(b, m_pattern, RegexOptions.IgnoreCase) ? 1.0 : 0.0;
    }
}