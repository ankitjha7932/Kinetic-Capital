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
        // 1. Check Cache first (Crucial for performance)
        string cacheKey = $"News_{symbol}";
        if (_cache.TryGetValue(cacheKey, out List<NewsArticle>? cachedNews))
        {
            return cachedNews ?? new List<NewsArticle>();
        }

        var articles = new List<NewsArticle>();
        try
        {
            // 2. THIS IS THE FIX: Point to Google RSS instead of Yahoo
            // We strip the .NS to make the search broader for Google News
            string cleanSymbol = symbol.Replace(".NS", "").Replace(".BO", "");
            string query = Uri.EscapeDataString($"{cleanSymbol} stock news India");
            string url = $"https://news.google.com/rss/search?q={query}&hl=en-IN&gl=IN&ceid=IN:en";

            // GetStreamAsync is better for XML/RSS 
            var response = await _httpClient.GetStreamAsync(url);
            
            using var xmlReader = XmlReader.Create(response);
            var feed = SyndicationFeed.Load(xmlReader);

            foreach (var item in feed.Items.Take(8)) 
            {
                // Google RSS Titles are usually "Headline - Publisher"
                string fullTitle = item.Title.Text;
                string source = "Google News";
                string cleanTitle = fullTitle;

                if (fullTitle.Contains(" - "))
                {
                    var parts = fullTitle.Split(" - ");
                    source = parts.Last().Trim();
                    cleanTitle = string.Join(" - ", parts.SkipLast(1)).Trim();
                }

                articles.Add(new NewsArticle(
                    Title: cleanTitle,
                    Description: item.Summary?.Text ?? cleanTitle,
                    Url: item.Links.FirstOrDefault()?.Uri.ToString() ?? "#",
                    Source: source,
                    ImageUrl: "", // Google RSS doesn't provide easy thumbnails
                    PublishedAt: item.PublishDate.DateTime
                ));
            }

            // 3. Store in cache for 30 mins to avoid hitting Google too hard
            _cache.Set(cacheKey, articles, TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google News RSS failed for {Symbol}", symbol);
        }

        return articles;
    }
}