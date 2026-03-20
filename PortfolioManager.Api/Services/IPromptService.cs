namespace PortfolioManager.Api.Services
{
    public interface IPromptService
    {
        string GetKineticStrategistPrompt(string? symbol, string message, string liveMarketContext);
    }
}