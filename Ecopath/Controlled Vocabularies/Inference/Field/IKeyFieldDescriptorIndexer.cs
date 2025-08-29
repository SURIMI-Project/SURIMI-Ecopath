using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;

namespace ControlledVocabularies.Inference.Field
{
    public interface IKeyFieldDescriptorIndexer
    {
        bool BuildIndex(string fieldName, IEnumerable<MultiLevelKey> records, KeyFieldDescriptor descriptor);
    }
}