using ControlledVocabularies.Core;

namespace ControlledVocabularies.Descriptors
{
    public interface IKeyFieldDescriptorRegistry
    {
        void Register(KeyFieldDescriptor descriptor);

        KeyFieldDescriptor? Get(KeyDomain domain, string fieldName);

        IEnumerable<KeyFieldDescriptor> GetAll(KeyDomain domain);
    }
}