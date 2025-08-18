using Ecopath.Utilities;

/// ===========================================================================
/// <summary>
/// Registry of named vocabularies.
/// </summary>
/// ===========================================================================
public class VocabularyRegistry
{
    private readonly Dictionary<string, IControlledVocabulary> m_vocabularies = new();

    /// <summary>
    /// Register a vocabulary of any type.
    /// </summary>
    public void Register(IControlledVocabulary vocab)
    {
        if (vocab == null) throw new ArgumentNullException(nameof(vocab));
        m_vocabularies[StringHelpers.NormalizeName(vocab.VocabularyName)] = vocab;
        vocab.Load();
    }

    public IEnumerable<IControlledVocabulary> GetCompatibleVocabularies(IControlledVocabulary source)
    {
        List<IControlledVocabulary> matches = new();
        GenericVocabularyMatcher m = new();

        foreach (IControlledVocabulary vocabulary in m_vocabularies.Values)
        {
            // No need for invariant intercomparisons, but hey
            if (MatchHelpers.CanMatch(source, vocabulary) && string.CompareOrdinal(source.VocabularyName, vocabulary.VocabularyName) != 0)
                matches.Add(vocabulary);
        }
        return matches;
    }

    /// <summary>
    /// Try to get a vocabulary by name.
    /// </summary>
    public IControlledVocabulary? Get(string name) 
    {
        m_vocabularies.TryGetValue(StringHelpers.NormalizeName(name), out IControlledVocabulary? vocab);
        return vocab;
    }

    /// <summary>
    /// Try to get a vocabulary by KeyDomain.
    /// </summary>
    public IEnumerable<IControlledVocabulary> GetByDomain(KeyDomain domain) =>
        m_vocabularies.Values.Where(v => v.KeyDomain == domain);

    /// <summary>
    /// Try to get a vocabulary by KeyPurpose
    /// </summary>
    public IEnumerable<IControlledVocabulary> GetByPurpose(KeyPurpose purpose) =>
        m_vocabularies.Values.Where(v => v.KeyPurpose == purpose);
}