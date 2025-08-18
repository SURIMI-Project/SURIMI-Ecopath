public static class MatchStrategyFactory
{
    public static IFieldMatcher Create(MatchStrategy strategy)
    {
        return strategy switch
        {
            MatchStrategy.Exact => new ExactFieldMatcher(),
            MatchStrategy.Fuzzy => new FuzzyFieldMatcher(),
            MatchStrategy.TokenOverlap => new TokenOverlapFieldMatcher(),
            _ => new ExactFieldMatcher()
        };
    }
}