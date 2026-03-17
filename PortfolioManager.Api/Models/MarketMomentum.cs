namespace PortfolioManager.Api.Models;

public record MarketMomentum(
    string Symbol,
    decimal Price,
    long Volume,
    decimal ValueTradedCr,
    decimal MarketCapCr,
    decimal HandoverRatio,
    decimal ChangePercent
);
