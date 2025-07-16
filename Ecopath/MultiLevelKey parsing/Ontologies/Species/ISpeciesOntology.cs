public interface ISpeciesOntology : IOntology
{
    (string match, int score) MatchSpeciesName(string speciesname);

    (string match, int score) MatchSpeciesCode(string speciescode);
}
