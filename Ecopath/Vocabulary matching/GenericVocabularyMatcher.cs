using Utilities;

public class GenericVocabularyMatcher : IVocabularyMatcher
{
    // ToDo: add smart parsing with the VocabularyFieldIndex

    /// <summary>
    /// Returns if two vocabularies are considered compatible for matching.
    /// </summary>
    /// <param name="sourceVocab"></param>
    /// <param name="targetVocab"></param>
    /// <returns></returns>
    public bool CanMatch(IControlledVocabulary sourceVocab, IControlledVocabulary targetVocab)
    {
        if (sourceVocab == null || targetVocab == null) return false;
        return (sourceVocab.KeyDomain == targetVocab.KeyDomain) && (sourceVocab.KeyPurpose == targetVocab.KeyPurpose);
    }

    /// <summary>
    /// Try to match a record from one vocabulary to another.
    /// </summary>
    /// <param name="record">The record to match.</param>
    /// <param name="vocabA">The source vocabulary that the record originates from.</param>
    /// <param name="vocabB">The target vocabulary that the record should be matched to.</param>
    /// <param name="minScore">The min score to accept a match.</param>
    /// <returns></returns>
    /// <remarks>
    /// This implementation uses <see cref="IControlledVocabulary.ForeignKeyMap"/> if present.
    /// This implementation does NOT yet use <see cref="IControlledVocabulary.FieldIndex"/>.
    /// </remarks>
    public VocabularyMatchResult Match(MultiLevelKey record, IControlledVocabulary vocabA, IControlledVocabulary vocabB, int minScore = 80)
    {
        VocabularyMatchResult best = VocabularyMatchResult.NoMatch();

        // Early bail-out
        if (vocabA == null || vocabB == null)
            return best;
        if (!CanMatch(vocabA, vocabB))
            return best;

        best = TryMatchFK(record, vocabA, vocabB);
        if (best.Score == 100) 
            return best;

        foreach (string nameIn in record.FieldNames)
        {
            string valueIn = record.GetField(nameIn)!.ToString(false) ?? string.Empty;
            if (string.IsNullOrEmpty(valueIn)) continue;

            foreach (var target in vocabB.Records)
                foreach (var nameTarget in target.FieldNames)
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

    #region Internals 

    private VocabularyMatchResult TryMatchFK(MultiLevelKey record, IControlledVocabulary vocabA, IControlledVocabulary vocabB)
    {
        // First, try to match any foreign key
        var matcher = new ExactFieldMatcher();
        var vocabBName = NameUtilities.NormalizeName(vocabB.VocabularyName);

        foreach (var kvp in vocabA.ForeignKeyMap)
        {
            string localField = kvp.Key;
            string foreignSpec = kvp.Value; // "VocabB[:Field]"
            if (string.IsNullOrEmpty(foreignSpec))
                continue;

            // Record FK field value cannot be null, is enforced in FK logic
            var fieldValue = record.GetField(localField)!.ToString(false);

            // Split FK target into vocab name and optional FK field name hint
            var tokens = foreignSpec.Split(':', StringSplitOptions.RemoveEmptyEntries);
            string foreignVocabName = NameUtilities.NormalizeName(tokens[0]);
            string foreignFieldHint = tokens.Length > 1 ? tokens[1] : string.Empty;

            // Is a key to provided vocabB?
            if (matcher.Score(vocabBName, foreignVocabName) == 1)
            {
                // Try specified field first (if any).
                // ToDo: use soon to appear FieldIndex to check if target field name exists in vocabB. For now, bluntly assess every record individually (which is wasteful)
                if (!string.IsNullOrEmpty(foreignFieldHint))
                {
                    foreach (var r in vocabB.Records)
                    {
                        var fieldTest = r.GetField(foreignFieldHint);
                        if ((fieldTest != null) && matcher.Score(fieldValue, fieldTest.ToString(false)) == 1)
                            return new VocabularyMatchResult() { Score = 100, Justification = "Matched via FK", MatchedRecord = r };
                    }
                    // ToDo: log this
                    Console.WriteLine("Can't find foreign key hint {0} in vocabulary {1}", foreignFieldHint, vocabB.VocabularyName);
                    
                    // foreignKeyHint failed but that could be expected; let's continue with brute-force key-by-key matching
                }

                // Fallback: check all fields
                foreach (MultiLevelKey r in vocabB.Records)
                    foreach (MultiLevelKeyField v in r.FieldValues)
                        if (matcher.Score(fieldValue, v.ToString(false)) == 1)
                            return new VocabularyMatchResult() { Score = 100, Justification = "Matched via FK, unnamed", MatchedRecord = r };
            }

            // No match found — maybe log or throw
        }
        return VocabularyMatchResult.NoMatch();
    }


    private int SimilarityScore(string a, string b)
    {
        // ToDo: deploy different matching strategies based on field lengths

        return NameUtilities.TokenSetFuzzyMatch(a, b);
    }

    # endregion // Internals
}
