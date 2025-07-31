public class VocabularyMatchResult
{
    public MultiLevelKey? MatchedRecord { get; set; }
    public int Score { get; set; }
    public string Justification { get; set; } = "";

    public static VocabularyMatchResult NoMatch() =>
        new() { MatchedRecord = null, Score = 0, Justification = "No suitable match" };
}