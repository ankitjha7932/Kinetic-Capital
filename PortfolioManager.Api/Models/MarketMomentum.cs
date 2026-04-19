namespace PortfolioManager.Api.Models;

public record MarketMomentum(
    string Symbol,
    string CompanyName,
    decimal Price,
    long Volume,
    decimal ValueTradedCr,
    decimal MarketCapCr,
    decimal Handover,
    decimal ChangePercent,
    decimal DayChange,
    decimal PreviousClose,
    decimal Return1W,
    decimal Return1M,
    List<decimal> Sparkline
);

/// <summary>
/// Strongly-typed response for GetIndexMoversAsync.
/// </summary>
public class IndexMoversResponse
{
    public bool Found { get; init; } = true;
    public string Index { get; init; } = string.Empty;
    public int TotalStocks { get; init; }
    public List<MarketMomentum> Gainers1D { get; init; } = new();
    public List<MarketMomentum> Losers1D { get; init; } = new();
    public List<MarketMomentum> VolumeShockers { get; init; } = new();
    public List<MarketMomentum> TopReturnsWeekly { get; init; } = new();
    public List<MarketMomentum> TopReturnsMonthly { get; init; } = new();
    public DateTime LastUpdated { get; init; }

    public static IndexMoversResponse NotFound(string indexName) =>
        new() { Found = false, Index = indexName.ToUpperInvariant() };
}
