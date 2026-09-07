namespace Ecopath.EwE.Prices;

// Prices per group
internal class EwEGroupPrices
{
    private int _group;
    private bool _calculated = false;

    public EwEGroupPrices(int group)
    {
        _group = group;
    }

    #region Initialization

    /// <summary>
    /// Base price, per fleet
    /// </summary>
    Dictionary<int, double> _startFleetPrice = new();

    /// <summary>
    /// Base landings, per fleet
    /// </summary>
    Dictionary<int, double> _startFleetLandings = new();

    /// <summary>
    /// Price scalars
    /// </summary>
    Dictionary<int, double> _basePriceScalars = new();

    /// <summary>
    /// Define the base price per fleet, and optionally the landings 
    /// to weight price calculations by.
    /// </summary>
    /// <param name="fleet"></param>
    /// <param name="price"></param>
    /// <param name="landings"></param>
    public void RegisterBasePrice(int fleet, double price, double landings = 1)
    {
        _startFleetPrice[fleet] = price;
        _startFleetLandings[fleet] = landings;

        _calculated = false;
    }

    public enum PriceDistribution
    {
        Mean,
        Max
    }

    public void CalculateBase(PriceDistribution method = PriceDistribution.Mean)
    {
        if (_calculated) return;

        double tot = 0;
        double count = 0;
        double max = 0;
        foreach (int fleet in _startFleetPrice.Keys)
        {
            tot += _startFleetPrice[fleet] * _startFleetLandings[fleet];
            count += 1;
            max = Math.Max(max, _startFleetPrice[fleet] * _startFleetLandings[fleet]);
        }

        if (count == 0) count = 1;

        double factor = 1;
        switch (method)
        {
            case PriceDistribution.Mean:
                factor = tot / Math.Max(count, 1);
                break;
            case PriceDistribution.Max:
                factor = max;
                break;
        }
        if (tot == 0) tot = 1;
        if (max == 0) max = 1;

        foreach (int fleet in _startFleetPrice.Keys)
        {
            // Per-fleet price scalar
            _basePriceScalars[fleet] = _startFleetPrice[fleet] * _startFleetLandings[fleet] / factor;
        }

        // Scalar across all fleets
        _basePriceScalars[0] = 1;
        _calculated = true;
    }

    #endregion // Initialization

    #region Runtime

    /// <summary>
    /// Evolving prices per fleet
    /// </summary>
    private Dictionary<int, double> _evolvingPrices = new();

    /// <summary>
    /// The proportions of evolving prices per fleet
    /// </summary>
    private Dictionary<int, double> _evolvingPriceProportions = new();

    private bool _PricesByFleet = false;

    public void AddEvolvingPrice(double price, int fleet = 0, double proportion = 1)
    {
        if (!_calculated)
            CalculateBase();

        _PricesByFleet |= (fleet > 0);

        // No silliness, please
        if (proportion <= 0) proportion = 1;

        _evolvingPrices[fleet] = _evolvingPrices.GetValueOrDefault(fleet) + price * proportion;
        _evolvingPriceProportions[fleet] = _evolvingPriceProportions.GetValueOrDefault(fleet) + proportion;

        _evolvingPrices[0] = _evolvingPrices.GetValueOrDefault(0) + price * proportion;
        _evolvingPriceProportions[0] = _evolvingPriceProportions.GetValueOrDefault(0) + proportion;
    }

    /// <summary>
    /// Apply the evolving prices to EwE, using the base price scalars to adjust the final price per fleet.
    /// The buffered totals are cleared after this operation.
    /// </summary>
    /// <param name="marketPrices"></param>
    public void ApplyToEwE(float[,] marketPrices)
    {
        if (!_calculated) return;

        foreach (int fleet in _startFleetPrice.Keys)
        {
            int srcFleet = _PricesByFleet ? fleet : 0;
            double weightedMeanEvolvingPrice = _evolvingPrices[srcFleet] / _evolvingPriceProportions[srcFleet];

            // The price to set in EwE
            double finalPrice = weightedMeanEvolvingPrice * _basePriceScalars[fleet];

            //// ToDo: set this in EwE
            //Console.WriteLine($"group {_group} x fleet {fleet} = price {finalPrice}");

            // ToDo: check bounds
            marketPrices[fleet, _group] = (float)finalPrice;
        }
        _evolvingPrices.Clear();
        _evolvingPriceProportions.Clear();
    }
    #endregion // Runtime
}
