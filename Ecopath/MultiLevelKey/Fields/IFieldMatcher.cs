/// <summary>
/// Interface for matching multilevel key field values
/// </summary>
public interface IFieldMatcher
{
    /// Returns a semantic similarity score between 0.0 and 1.0
    double Score(string valueA, string valueB);
}