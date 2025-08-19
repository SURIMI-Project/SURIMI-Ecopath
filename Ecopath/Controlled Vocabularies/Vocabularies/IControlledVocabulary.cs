using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;

namespace ControlledVocabularies.Vocabularies
{
    /// <summary>
    /// Defines a publicly used controlled vocabulary.
    /// </summary>
    /// <todo>Add type safe / reflection friendliness, e.g., Type RecordType { get; }
    /// </todo>
    /// <todo>Support optional metadata fields, e.g, string? SourceUri { get; }; DateTime? LoadedAt { get; }; bool IsDirty { get; }; etc.</todo>
    /// <todo>
    /// Add support for namespaces, e.g., 'marine:FAO', 'terrestrial:GBIF', or user-defined vocab groups.
    /// Could include a 'string Namespace { get; }' or compound key like 'Namespace:VocabularyName'.
    /// </todo>
    /// </list>
    /// </todo>
    public interface IControlledVocabulary
    {
        /// <summary>
        /// Get what kind of thing this vocabulary is about.
        /// </summary>
        KeyDomain KeyDomain { get; }

        /// <summary>
        /// Get why this vocabulary exists / how it’s used
        /// </summary>
        KeyPurpose KeyPurpose { get; }

        /// <summary>
        /// The unique name of the vocabulary as commonly indicated.
        /// </summary>
        /// <todo>
        /// This API should support aliases. Consider changing this to a 'List<string> Aliases { get; }', 
        /// where the first element is the standard name. Adapt vocabulary lookup and registration accordingly.
        /// </todo>
        string VocabularyName { get; }

        /// <summary>
        /// Get the data in the vocabulary.
        /// </summary>
        IEnumerable<MultiLevelKey> Records { get; }

        /// <summary>
        /// Get the field names in the vocabulary.
        /// </summary>
        IEnumerable<string> FieldNames { get; }

        /// <summary>
        /// Get the field name that holds the unique code field.
        /// </summary>
        string CodeFieldName { get; }

        /// <summary>
        /// Get the <see cref="KeyFieldDescriptor"/> that holds the interpretation
        /// of the data for a given field.
        /// </summary>
        /// <param name="FieldName"></param>
        /// <returns>A <see cref="KeyFieldDescriptor"/>, or null if the fieldname
        /// does not exist.</returns>
        /// <todo>
        /// Lazy index, only index the data when a KeyFieldDescriptor is first requested?
        /// </todo>
        KeyFieldDescriptor? GetKeyFieldDescriptor(string FieldName);

        /// <summary>
        /// Get keys to other controlled vocabularies. This data is 
        /// expressed as Dictionary(fieldName -> [vocabulary_name]:[field_name]) to facilitate direct look-ups.
        /// </summary>
        /// <todo>
        /// Rather than exposing the dictionary, let's allow interactions solely through accessor methods
        /// </todo>
        Dictionary<string, string> ForeignKeyMap { get; }

        /// <summary>
        /// Load the vocabulary.
        /// </summary>
        /// <returns>True if successful.</returns>
        /// <todo>
        /// Consider adding support for async loading and external metadata injection.
        /// </todo>
        bool Load();

        /// <summary>
        /// Find the <see cref="CodeFieldName"/> value for a given string anywhere in the vocabulary.
        /// Uses strategy-based semantic matching to infer the most likely match.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        string FindCode(string input);
    }
}