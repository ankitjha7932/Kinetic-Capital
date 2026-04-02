using System.Globalization;
using MongoDB.Driver;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public class PeerComparisonService
{
    private readonly IMongoCollection<StockFundamental> _fundamentalCollection;
    private readonly CultureInfo _indianCulture = new CultureInfo("en-IN");

    public PeerComparisonService(IMongoDatabase database)
    {
        _fundamentalCollection = database.GetCollection<StockFundamental>("StocksDeepData");
    }

    public async Task<object?> GetPeerIntelligenceAsync(string symbol)
    {
        var filter = Builders<StockFundamental>.Filter.Eq(s => s.Symbol, symbol);
        var projection = Builders<StockFundamental>
            .Projection.Include(s => s.Symbol)
            .Include(s => s.Industry)
            .Include(s => s.CompanyName)
            .Include(s => s.PeersData);

        var stockDoc = await _fundamentalCollection
            .Find(filter)
            .Project<StockFundamental>(projection)
            .FirstOrDefaultAsync();

        if (stockDoc == null || stockDoc.PeersData == null || stockDoc.PeersData.Count == 0)
            return null;

        return new
        {
            Symbol = stockDoc.Symbol,
            Industry = stockDoc.Industry,
            CompanyName = stockDoc.CompanyName,
            Peers = stockDoc
                .PeersData.Select(p => new
                {
                    p.Name,
                    p.Symbol,
                    PE = FormatIndianNumber(p.PE),
                    MarketCap = FormatIndianNumber(p.MarketCap),
                    DivYield = FormatIndianNumber(p.DivYield, isPercentage: true),
                    NetProfitQtr = FormatIndianNumber(p.NetProfitQtr),
                    ProfitVarQtr = FormatIndianNumber(p.ProfitVarQtr, isPercentage: true),
                    SalesQtr = FormatIndianNumber(p.SalesQtr),
                    SalesVarQtr = FormatIndianNumber(p.SalesVarQtr, isPercentage: true),
                    ROCE = FormatIndianNumber(p.ROCE, isPercentage: true),
                    IsCurrent = (
                        p.Symbol != null
                        && p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)
                    )
                        || (
                            stockDoc.CompanyName.Length >= 5
                            && p.Name.Contains(stockDoc.CompanyName.Substring(0, 5))
                        ),
                })
                .ToList(),
        };
    }

    private string FormatIndianNumber(string rawValue, bool isPercentage = false)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || rawValue == "N/A")
            return "—";

        if (double.TryParse(rawValue, out double numericValue))
        {
            string formatted = numericValue.ToString("N2", _indianCulture);

            return isPercentage ? $"{formatted}%" : formatted;
        }

        return rawValue;
    }
}
