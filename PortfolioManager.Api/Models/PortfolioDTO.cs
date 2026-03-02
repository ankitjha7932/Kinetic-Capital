using System;  // ← ADD THIS (DateTime for HoldingResponse)

namespace PortfolioManager.Api.Models;

public record RegisterRequest(string Email, string Password, string Otp, string RiskProfile, int InvestmentHorizon, string[] PreferredSectors);
public record LoginRequest(string Email, string Password);
public record HoldingRequest(string UserId, string Symbol, decimal Quantity, decimal AvgBuyPrice, DateTime? PurchaseDate = null, string? Tags = null);
public record HoldingUpdateRequest(decimal Quantity, decimal AvgBuyPrice);
public record HoldingResponse(string Id, string Symbol, decimal Quantity, decimal AvgBuyPrice, decimal CurrentPrice, decimal UnrealizedPnl, DateTime PurchaseDate, string? Tags);
public class PortfolioSummaryResponse
{
    public string UserId { get; set; }
    public int TotalHoldings { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal TotalPnl { get; set; }
    public List<HoldingResponse> Holdings { get; set; } = new();
}

public record NewsArticle(string Title, string Description, string Url, string Source, string ImageUrl, DateTime PublishedAt);
