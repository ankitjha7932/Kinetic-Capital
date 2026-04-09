using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Caching.Memory;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public class NewsService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NewsService> _logger;

    public NewsService(HttpClient httpClient, IMemoryCache cache, ILogger<NewsService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<NewsArticle>> GetStockNewsAsync(string symbol)
    {
        string cacheKey = $"News_{symbol}";
        if (_cache.TryGetValue(cacheKey, out List<NewsArticle>? cachedNews))
        {
            return cachedNews ?? new List<NewsArticle>();
        }

        var articles = new List<NewsArticle>();
        try
        {
            string cleanSymbol = symbol.Replace(".NS", "").Replace(".BO", "");
            string query = Uri.EscapeDataString($"{cleanSymbol} stock news India");
            string url = $"https://news.google.com/rss/search?q={query}&hl=en-IN&gl=IN&ceid=IN:en";

            // Minimal change: Added WaitAsync to prevent hanging threads and 502 errors
            var response = await _httpClient.GetStreamAsync(url).WaitAsync(TimeSpan.FromSeconds(8));

            using var xmlReader = XmlReader.Create(response);
            var feed = SyndicationFeed.Load(xmlReader);

            foreach (var item in feed.Items.OrderByDescending(i => i.PublishDate).Take(8))
            {
                string fullTitle = item.Title.Text;
                string source = "Google News";
                string cleanTitle = fullTitle;

                if (fullTitle.Contains(" - "))
                {
                    var parts = fullTitle.Split(" - ");
                    source = parts.Last().Trim();
                    cleanTitle = string.Join(" - ", parts.SkipLast(1)).Trim();
                }

                articles.Add(
                    new NewsArticle(
                        Title: cleanTitle,
                        Description: item.Summary?.Text ?? cleanTitle,
                        Url: item.Links.FirstOrDefault()?.Uri.ToString() ?? "#",
                        Source: source,
                        ImageUrl: "",
                        PublishedAt: item.PublishDate.DateTime
                    )
                );
            }

            // Store in cache for 30 mins avod hiting Google so frequenyly
            _cache.Set(cacheKey, articles, TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google News RSS failed for {Symbol}", symbol);
        }

        return articles;
    }
}
