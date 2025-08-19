namespace ControlledVocabularies.Match
{
    public class NumericFieldMatcher : IFieldMatcher
    {
        public double Score(string a, string b)
        {
            if (double.TryParse(a, out double valA) && double.TryParse(b, out double valB))
            {
                double diff = Math.Abs(valA - valB);
                return diff == 0 ? 1.0 : 1.0 / (1.0 + diff);  // inverse distance
            }
            return 0;
        }
    }
}