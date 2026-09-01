using System.Diagnostics;

namespace Ecopath.EwE.Prices;

internal class PriceBridge
{
    Dictionary<string, EwEMarketPricesManager> _markets = new();
    Dictionary<int, string> _fleetMarkets = new();

    private float[,] _marketPrices;

    public PriceBridge(float[,] marketPrices)
    {
        _marketPrices = marketPrices;
    }

    /// <summary>
    /// First, set up the market + fleet structure. 
    /// </summary>
    /// <param name="market"></param>
    /// <param name="fleet"></param>
    public void MapFleetToMarket(string market, int fleet)
    {
        market = NormalizedMarketCode(market);

        Debug.Assert(!string.IsNullOrEmpty(market));
        Debug.Assert(fleet > 0);
        Debug.Assert(!_fleetMarkets.ContainsKey(fleet));

        _fleetMarkets[fleet] = market;
        GetMarket(market, true);
    }

    /// <summary>
    /// Second, register the various base prices.
    /// </summary>
    /// <param name="group"></param>
    /// <param name="fleet"></param>
    /// <param name="price"></param>
    /// <param name="landings"></param>
    public void SetBasePrice(int group, int fleet, double price, double landings = 1)
    {
        var market = GetMarket(fleet);
        Debug.Assert(market != null);
        market!.RegisterBasePrice(group, fleet, price, landings);
    }

    public void AddEvolvingPrice(string market, int group, double price, int fleet = 0, double proportion = 1)
    {
        market = NormalizedMarketCode(market);
        if (fleet > 0)
            Debug.Assert(_fleetMarkets[fleet] == market);

        var mp = GetMarket(market, false);
        Debug.Assert(mp != null);
        mp!.AddEvolvingPrice(group, price, fleet, proportion);
    }

    public void AppyToEwE()
    {
        foreach (EwEMarketPricesManager mp in _markets.Values)
            mp.ApplyToEwE(_marketPrices);
    }

    #region Internal helpers

    private EwEMarketPricesManager GetMarket(string market, bool create)
    {
        market = NormalizedMarketCode(market);
        if (create)
            _markets.TryAdd(market, new EwEMarketPricesManager(market));
        return _markets[market];
    }

    private EwEMarketPricesManager? GetMarket(int fleet)
    {
        return _fleetMarkets.TryGetValue(fleet, out var market)
            ? GetMarket(market, false)
            : null;
    }

    private string NormalizedMarketCode(string market)
    {
        return String.IsNullOrWhiteSpace(market) ? "na" : market.ToLowerInvariant();
    }

    #endregion // Internal helpers
}
