public class VocabularyFieldIndex : IVocabularyFieldIndex
{
    public static VocabularyFieldIndex FromData(IEnumerable<MultiLevelKey> values)
    {
        return new VocabularyFieldIndex();
    }
}
