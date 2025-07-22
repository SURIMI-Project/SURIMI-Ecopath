using Ecopath.EwE;

public class MultiLevelKeyMatch
{
    public MultiLevelKeyMatch(MultiLevelKey key, int score)
    {
        this.Key = key;
        this.Score = score;
    }
    public MultiLevelKey Key { get; }
    public int Score { get; }
}
