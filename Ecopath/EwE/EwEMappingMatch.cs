public class EwEMappingMatch
{
    public EwEMappingMatch(EwEMapping mapping, int score) 
    { 
        this.EwEMapping = mapping;
        this.Score = score;
    }
    public EwEMapping EwEMapping { get; }
    public int Score { get; }
}
