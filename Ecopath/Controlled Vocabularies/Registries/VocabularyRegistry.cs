using ControlledVocabularies.Core;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;

public class VocabularyRegistry : IVocabularyRegistry
{
    private readonly Dictionary<string, IControlledVocabulary> m_vocabularies = new();

    public void Register(IControlledVocabulary vocab)
    {
        ArgumentNullException.ThrowIfNull(vocab);
        m_vocabularies[StringHelpers.NormalizeName(vocab.VocabularyName)] = vocab;
        vocab.Load();
    }

    public bool Unregister(string name)
        => m_vocabularies.Remove(StringHelpers.NormalizeName(name));

    public bool Unregister(IControlledVocabulary vocab)
    {
        ArgumentNullException.ThrowIfNull(vocab);
        return Unregister(vocab.VocabularyName);
    }
    public void Clear() => m_vocabularies.Clear();

    public IControlledVocabulary? Get(string name)
        => m_vocabularies.TryGetValue(StringHelpers.NormalizeName(name), out var v) ? v : null;

    public bool TryGetByNameOrAlias(string name, out IControlledVocabulary? vocab)
        => m_vocabularies.TryGetValue(StringHelpers.NormalizeName(name), out vocab);

    public IEnumerable<IControlledVocabulary> GetByDomain(KeyDomain domain)
        => m_vocabularies.Values.Where(v => v.Domain == domain);

    public IEnumerable<IControlledVocabulary> GetByPurpose(KeyPurpose purpose)
        => m_vocabularies.Values.Where(v => v.Purpose == purpose);

    public IEnumerable<IControlledVocabulary> GetCompatibleVocabularies(IControlledVocabulary source)
        => m_vocabularies.Values.Where(v => MatchHelpers.CanMatch(source, v) && !ReferenceEquals(source, v));
}