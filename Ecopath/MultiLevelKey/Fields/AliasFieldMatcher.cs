/// <summary>
/// Alias-based matcher using predefined mappings. Not sure if we need this
/// </summary>
public class AliasMatcher : IFieldMatcher
{
    private readonly Dictionary<string, HashSet<string>> m_aliases = new(StringComparer.OrdinalIgnoreCase);

    public void AddAliases(string canonical, IEnumerable<string> aliases)
    {
        if (!m_aliases.TryGetValue(canonical, out var set))
            m_aliases[canonical] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in aliases)
            set.Add(alias);
    }

    public double Score(string valueA, string valueB)
    {
        foreach (var kvpAliases in m_aliases)
        {
            var key = kvpAliases.Key;
            var aliasSet = kvpAliases.Value;

            // Find literal match, not case sensitive
            if ((string.Equals(valueA, key, StringComparison.OrdinalIgnoreCase) && aliasSet.Contains(valueB)) ||
                (string.Equals(valueB, key, StringComparison.OrdinalIgnoreCase) && aliasSet.Contains(valueA)) ||
                (aliasSet.Contains(valueA) && aliasSet.Contains(valueB)))
                return 1.0;
        }
        return 0.0;
    }
}