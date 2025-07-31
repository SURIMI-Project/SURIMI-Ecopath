using Utilities;

public class GenericVocabularyMatcher : IVocabularyMatcher
{
    public bool CanMatch(IControlledVocabulary sourceVocab, IControlledVocabulary targetVocab)
    {
        if (sourceVocab == null || targetVocab == null) return false;
        return (sourceVocab.KeyDomain == targetVocab.KeyDomain) && (sourceVocab.KeyPurpose == targetVocab.KeyPurpose);
    }

    public VocabularyMatchResult Match(MultiLevelKey input, IControlledVocabulary sourceVocab, IControlledVocabulary targetVocab, int minScore = 80)
    {
        VocabularyMatchResult best = VocabularyMatchResult.NoMatch();

        foreach (string nameIn in input.FieldNames())
        {
            string valueIn = input.GetField(nameIn)!.ToString(false) ?? string.Empty;
            if (string.IsNullOrEmpty(valueIn)) continue;

            foreach (var target in targetVocab.Records)
                foreach (var nameTarget in target.FieldNames())
                {
                    string valueTarget = target.GetField(nameTarget)!.ToString(false) ?? string.Empty;
                    if (string.IsNullOrEmpty(valueTarget)) continue;

                    var score = SimilarityScore(valueIn, valueTarget);
                    if (score > best.Score)
                    {
                        best = new VocabularyMatchResult
                        {
                            MatchedRecord = target,
                            Score = score,
                            Justification = $"Matched on name with score {score}"
                        };
                    }
                }
        }

        return best.Score >= minScore ? best : VocabularyMatchResult.NoMatch();
    }

    private int SimilarityScore(string a, string b)
    {
        // ToDo: deploy different matching strategies based on field lengths

        return NameUtilities.TokenSetFuzzyMatch(a, b);
    }

}
