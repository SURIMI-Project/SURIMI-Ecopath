public interface ILifeStageVocabulary : IControlledVocabulary
{
    (string match, int score) MatchLifestage(string stage, int iMinScore = 70);

    /// <summary>
    /// Map a code in the vocabulary to a life stage name.
    /// </summary>
    /// <param name="lifeStageCode"></param>
    /// <returns></returns>
    string CodeToLifeStage(string lifeStageCode);

    /// <summary>
    /// Map a life stage name to a code in the vocabulary.
    /// </summary>
    /// <param name="LifeStage"></param>
    /// <returns></returns>
    string LifeStageToCode(string LifeStage);
}
