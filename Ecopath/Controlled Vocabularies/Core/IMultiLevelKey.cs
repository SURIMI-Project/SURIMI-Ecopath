using ControlledVocabularies.Descriptors;

namespace ControlledVocabularies.Core
{
    /// <summary>
    /// Represents a multi-level, self-describing key (e.g., for species or fleets)
    /// </summary>
    public interface IMultiLevelKey
    {
        /// <summary>
        /// Get the knowledge domain.
        /// </summary>
        KeyDomain Domain { get; }
        IEnumerable<string> FieldNames { get; }
        DateTime TimeStamp { get; set; }

        /// <summary>
        /// Get the knowledge domain of the given field.
        /// </summary>
        KeyDomain FieldDomain(string field);
        /// <summary>
        /// Get the knowledge purpose of the given field.
        /// </summary>
        KeyPurpose FieldPurpose(string field);

        /// <summary>
        /// Get the <see cref="IKeyFieldDescriptor"> for the given field.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        IMultiLevelKeyField? GetField(string key);

        /// <summary>
        /// Parse a structured key string into fields and values.
        /// </summary>
        /// <param name="keyStr"></param>
        /// <returns></returns>
        bool Parse(string keyStr);

        /// <summary>
        /// Set a field to a specific value of the form "varname={vocab:}value"
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="bPurgeVocabularyName">If true, strips out the vocabulary name from the value</param>
        void SetField(string key, string value, bool bPurgeVocabularyName = false);

        /// <summary>
        /// ASsociate a <see cref="IKeyFieldDescriptor"/> to the given field.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="descriptor"></param>
        void SetFieldDescriptor(string key, IKeyFieldDescriptor? descriptor);

        /// <summary>
        /// Factory method, instantiate an object with field values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="includeVocabulary"></param>
        /// <returns></returns>
        T? ToObject<T>(bool includeVocabulary = true);
    }
}