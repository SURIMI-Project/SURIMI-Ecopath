public interface ICountryCodeVocabulary : IControlledVocabulary
{
    /// <summary>
    /// Resolve a country name to the official country name.
    /// </summary>
    /// <param name="countryname"></param>
    /// <returns></returns>
    (string match, int score) MatchCountryName(string countryname, int iMinScore = 70);

    /// <summary>
    /// Map a code in the vocabulary to a country name.
    /// </summary>
    /// <param name="countrycode"></param>
    /// <returns></returns>
    string CodeToCountrys(string countrycode);

    /// <summary>
    /// Map a country  name to a code in the vocabulary.
    /// </summary>
    /// <param name="countryname"></param>
    /// <returns></returns>
    string CountryToCode(string countryname);

}
