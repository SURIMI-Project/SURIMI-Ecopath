public interface ILifestageVocabulary : IControlledVocabulary
{
    (string match, int score) MatchLifestage(string stage, int iMinScore = 70);
}
