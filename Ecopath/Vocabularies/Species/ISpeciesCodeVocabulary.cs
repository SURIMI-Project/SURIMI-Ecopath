public interface ISpeciesCodeVocabulary : IControlledVocabulary
{
    /// <summary>
    /// Resolve a species name to the properly spelled species name.
    /// </summary>
    /// <param name="speciesname"></param>
    /// <returns></returns>
    (string match, int score) MatchSpeciesName(string speciesname, int iMinScore = 70);

    /// <summary>
    /// Map a code in the vocabulary to a species name.
    /// </summary>
    /// <param name="speciescode"></param>
    /// <returns></returns>
    string CodeToSpecies(string speciescode);

    /// <summary>
    /// Map a species name to a code in the vocabulary.
    /// </summary>
    /// <param name="speciesname"></param>
    /// <returns></returns>
    string SpeciesToCode(string speciesname);

}
