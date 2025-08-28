namespace ControlledVocabularies.Core
{
    /// <summary>
    /// Helper, mediates between a multi-level field, its vocabulary and its value.
    /// </summary>

    public interface IMultiLevelKeyField 
    {
        string Value { get; set; }
        string Vocabulary { get; set; }

        string ToString(bool includeVocabulary = true);
    }
}