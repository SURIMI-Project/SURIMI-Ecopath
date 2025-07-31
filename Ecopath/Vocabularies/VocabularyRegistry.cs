using Utilities;

/// =====================================================
/// <summary>
/// Registry of named vocabularies, organized by <see cref="KeyDomain"/>
/// </summary>
public class VocabularyRegistry
{
    private readonly Dictionary<string, IControlledVocabulary> m_vocabularies = new();

    /// <summary>
    /// Register a vocabulary of any type.
    /// </summary>
    public void Register(IControlledVocabulary vocab)
    {
        if (vocab == null) throw new ArgumentNullException(nameof(vocab));
        m_vocabularies[NameUtilities.NormalizeName(vocab.VocabularyName)] = vocab;
    }

    /// <summary>
    /// Try to get a vocabulary of type T by name.
    /// </summary>
    public bool TryGet<T>(string name, out T? vocab) where T : class, IControlledVocabulary
    {
        if (m_vocabularies.TryGetValue(NameUtilities.NormalizeName(name), out var found))
        {
            vocab = found as T;
            if (vocab != null)
            { 
                // Load when obtained?
                vocab.Load();
                return true;
            }
        }

        vocab = null;
        return false;
    }

    /// <summary>
    /// Get a vocabulary of type T by name or throw.
    /// </summary>
    public T Get<T>(string name) where T : class, IControlledVocabulary
    {
        if (!TryGet<T>(NameUtilities.NormalizeName(name), out var vocab))
            throw new InvalidOperationException(
                $"Vocabulary '{name}' not found or not of type {typeof(T).Name}.");
          return vocab!;
    }

    /// <summary>
    /// List all registered vocabularies (names and types).
    /// </summary>
    public IEnumerable<(string Name, Type Type)> List()
    {
        return m_vocabularies.Select(kv => (kv.Key, kv.Value.GetType()));
    }
}