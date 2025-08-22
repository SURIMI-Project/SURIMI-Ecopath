using ControlledVocabularies.Core;

namespace ControlledVocabularies.Descriptors
{
    public interface IKeyFieldIndexer
    {
        bool BuildIndex(string fieldName,
                        IEnumerable<MultiLevelKey> records,
                        KeyFieldDescriptor descriptor);
    }
}