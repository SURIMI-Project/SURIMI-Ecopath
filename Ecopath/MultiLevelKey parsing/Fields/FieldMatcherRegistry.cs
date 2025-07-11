/// <summary>
/// Field-specific matchers
/// </summary>
public class MatcherRegistry
{
    private readonly Dictionary<string, IFieldMatcher> m_matchers = new();

    public void Register(string field, IFieldMatcher matcher) =>
        m_matchers[field.ToLower()] = matcher;

    public IFieldMatcher? Get(string field) =>
        m_matchers.TryGetValue(field.ToLower(), out var matcher) ? matcher : new ExactFieldMatcher(); // Always return a default
}