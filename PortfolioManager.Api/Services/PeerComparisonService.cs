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
                    // 1. P/E Ratio
                    PE = FormatIndianNumber(p.PE),

                    // 2. Market Cap (Rs. Cr.)
                    MarketCap = FormatIndianNumber(p.MarketCap),

                    // 3. Dividend Yield (%)
                    DivYield = FormatIndianNumber(p.DivYield, isPercentage: true),

                    // 4. Net Profit Quarter (Rs. Cr.)
                    NetProfitQtr = FormatIndianNumber(p.NetProfitQtr),

                    // 5. Quarterly Profit Variance (%)
                    ProfitVarQtr = FormatIndianNumber(p.ProfitVarQtr, isPercentage: true),

                    // 6. Sales Quarter (Rs. Cr.)
                    SalesQtr = FormatIndianNumber(p.SalesQtr),

                    // 7. Quarterly Sales Variance (%)
                    SalesVarQtr = FormatIndianNumber(p.SalesVarQtr, isPercentage: true),

                    // 8. ROCE (%)
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

        string cleanValue = rawValue.Replace(",", "").Replace("%", "").Trim();

        if (
            double.TryParse(
                cleanValue,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double numericValue
            )
        )
        {
            string formatted = numericValue.ToString("N2", _indianCulture);
            return isPercentage ? $"{formatted}%" : formatted;
        }

        return rawValue;
    }
}
