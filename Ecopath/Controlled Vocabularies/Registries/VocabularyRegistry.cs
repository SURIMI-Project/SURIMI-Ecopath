using ControlledVocabularies.Core;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Registries
{

    /// <summary>
    /// 
    /// </summary>
    /// <todo>Make thread-safe (ConcurrentDictionary or RW lock); expose snapshot enumeration.</todo>
    /// <todo>Support aliases (normalized) and collisions resolution policy; add events OnRegistered/OnUnregistered.</todo>
    /// <todo>Add GetCompatibleVocabularies(source, enforceKind?:bool) leveraging FieldKind if desired.</todo>
    public class VocabularyRegistry : IVocabularyRegistry
    {
        private readonly Dictionary<string, IControlledVocabulary> m_vocabularies = new();

        public void Register(IControlledVocabulary vocab, string? name = null)
        {
            ArgumentNullException.ThrowIfNull(vocab);

            string registryName = name ?? vocab.VocabularyName;
            m_vocabularies[FieldPolicy.ForSchema(registryName)] = vocab;
            vocab.Load();
        }

        public bool Unregister(string name)
            => m_vocabularies.Remove(FieldPolicy.ForSchema(name));

        public bool Unregister(IControlledVocabulary vocab)
        {
            ArgumentNullException.ThrowIfNull(vocab);
            return Unregister(vocab.VocabularyName);
        }

        public void Clear() => m_vocabularies.Clear();

        public IControlledVocabulary? Get(string name)
            => m_vocabularies.TryGetValue(FieldPolicy.ForSchema(name), out var v) ? v : null;

        public bool TryGetByNameOrAlias(string name, out IControlledVocabulary? vocab)
            => m_vocabularies.TryGetValue(FieldPolicy.ForSchema(name), out vocab);

        public IEnumerable<IControlledVocabulary> GetByDomain(KeyDomain domain)
            => m_vocabularies.Values.Where(v => v.Domain == domain);

        public IEnumerable<IControlledVocabulary> GetByPurpose(KeyPurpose purpose)
            => m_vocabularies.Values.Where(v => v.Purpose == purpose);

        public IEnumerable<IControlledVocabulary> GetCompatibleVocabularies(IControlledVocabulary source)
            => m_vocabularies.Values.Where(v => MatchHelpers.CanMatch(source, v) && !ReferenceEquals(source, v));
    }
}