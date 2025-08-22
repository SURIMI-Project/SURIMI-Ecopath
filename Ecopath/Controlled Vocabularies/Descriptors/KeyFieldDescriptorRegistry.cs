using ControlledVocabularies.Core;

namespace ControlledVocabularies.Descriptors
{
    /// <summary>
    /// Field-specific descriptors, organized per <see cref="KeyDomain"/>.
    /// </summary>
    /// <todo>Return read-only snapshots for GetAll(domain); avoid exposing internal mutables.</todo>
    /// <todo>Make thread-safe (concurrent reads) or document single-writer, multi-reader expectations.</todo>
    public class KeyFieldDescriptorRegistry : IKeyFieldDescriptorRegistry
    {
        private readonly Dictionary<(KeyDomain, string), KeyFieldDescriptor> Descriptors = new();

        public void Register(KeyFieldDescriptor descriptor)
        {
            var key = (descriptor.Domain, descriptor.FieldName);
            Descriptors[key] = descriptor;
        }

        public KeyFieldDescriptor? Get(KeyDomain domain, string fieldName)
        {
            Descriptors.TryGetValue((domain, fieldName), out var descriptor);
            return descriptor;
        }

        public IEnumerable<KeyFieldDescriptor> GetAll(KeyDomain domain)
        {
            return Descriptors
                .Where(kvp => kvp.Key.Item1 == domain)
                .Select(kvp => kvp.Value);
        }
    }
}