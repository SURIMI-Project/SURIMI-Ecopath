public interface IGearCodeVocabulary : IControlledVocabulary
{
    /// <summary>
    /// Resolve a gear name to the official gear name.
    /// </summary>
    /// <param name="gearname"></param>
    /// <returns></returns>
    (string match, int score) MatchGearName(string gearname, int iMinScore = 70);

    /// <summary>
    /// Map a code in the vocabulary to a gear name.
    /// </summary>
    /// <param name="gearcode"></param>
    /// <returns></returns>
    string CodeToGears(string gearcode);

    /// <summary>
    /// Map a gear  name to a code in the vocabulary.
    /// </summary>
    /// <param name="gearname"></param>
    /// <returns></returns>
    string GearToCode(string gearname);
}
