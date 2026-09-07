using System.Diagnostics;

namespace Ecopath.EwE.Prices;

internal class EwEMarketPricesManager
{
    string _market = "";

    public EwEMarketPricesManager(string market)
    {
        _market = market;
    }

    #region Initialization

    private Dictionary<int, EwEGroupPrices> _groupPrices = new();

    /// <summary>
    /// Add an EwE price record
    /// </summary>
    /// <param name="igroup"></param>
    /// <param name="fleet"></param>
    /// <param name="price"></param>
    /// <param name="propLandings">Optional base landings proportion</param>
    public void RegisterBasePrice(int group, int fleet, double price, double propLandings = 1.0)
    {
        GetPrices(group, true).RegisterBasePrice(fleet, price, propLandings);
    }

    public void CalculateBase()
    {
        foreach (EwEGroupPrices price in _groupPrices.Values)
            price.CalculateBase();
    }

    private EwEGroupPrices GetPrices(int group, bool create)
    {
        if (!_groupPrices.ContainsKey(group) && create)
            _groupPrices[group] = new EwEGroupPrices(group);
        return _groupPrices[group];
    }

    #endregion // Initialization

    public void AddEvolvingPrice(int group, double price, int fleet = 0, double proportion = 1)
    {
        EwEGroupPrices prices = GetPrices(group, false);
        Debug.Assert(prices != null);
        prices.AddEvolvingPrice(price, fleet, proportion);
    }

    public void ApplyToEwE(float[,] marketPrices)
    {
        foreach (EwEGroupPrices gp in _groupPrices.Values)
            gp.ApplyToEwE(marketPrices);
    }
}
