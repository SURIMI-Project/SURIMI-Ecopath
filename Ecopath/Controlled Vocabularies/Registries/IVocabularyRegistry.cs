using ControlledVocabularies.Core;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Registries
{
    /// <summary>
    ///  Interface for defining vocabulary registries
    /// </summary>
    public interface IVocabularyRegistry
    {
        /// <summary>
        /// Add a vocabulary to the registry.
        /// </summary>
        /// <param name="vocab"></param>
        void Register(IControlledVocabulary vocab, string? name = null);

        /// <summary>
        /// Remove a vocabulary from the registry.
        /// </summary>
        /// <param name="vocab"></param>
        bool Unregister(string name);

        /// <summary>
        /// Remove a vocabulary from the registry.
        /// </summary>
        /// <param name="vocab"></param>
        bool Unregister(IControlledVocabulary vocab);

        /// <summary>
        /// Clear the registry.
        /// </summary>
        void Clear();

        /// <summary>
        /// Get a vocabulary by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IControlledVocabulary? Get(string name);

        /// <summary>
        /// Try to get a vocabulary by name, also allowing for aliases.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="vocab"></param>
        /// <returns></returns>
        bool TryGetByNameOrAlias(string name, out IControlledVocabulary? vocab);

        /// <summary>
        /// Get all vocabularies for a given <see cref="KeyDomain"/>
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        IEnumerable<IControlledVocabulary> GetByDomain(KeyDomain domain);

        /// <summary>
        /// Get all vocabularies for a given <see cref="KeyPurpose"/>
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        IEnumerable<IControlledVocabulary> GetByPurpose(KeyPurpose purpose);

        /// <summary>
        /// Get all vocabularies that are compatible with a given vocabulary.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        IEnumerable<IControlledVocabulary> GetCompatibleVocabularies(IControlledVocabulary source);
    }
}
