public interface IStageOntology : IOntology
{
    (string match, int score) MatchStage(string stage, int iMinScore = 70);
}
