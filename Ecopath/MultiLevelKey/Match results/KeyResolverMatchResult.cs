public class KeyResolverMatchResult : MatchResult
{
    public KeyResolverMatchResult(IEnumerable<MatchResult> matches, int score)
    {
        Matches = matches;
        Score = score;
    }

    public IEnumerable<MatchResult> Matches { get; }

    public new static KeyResolverMatchResult NoMatch => new([], 0);
}
