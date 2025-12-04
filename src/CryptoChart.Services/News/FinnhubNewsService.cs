using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoChart.Services.News;

/// <summary>
/// Finnhub API client for fetching cryptocurrency news.
/// API Documentation: https://finnhub.io/docs/api/market-news
/// </summary>
public class FinnhubNewsService : INewsService
{
    private readonly HttpClient _httpClient;
    private readonly FinnhubSettings _settings;
    private readonly ILogger<FinnhubNewsService> _logger;
    private readonly SemaphoreSlim _rateLimiter;
    private DateTime _lastRequestTime = DateTime.MinValue;

    public FinnhubNewsService(
        HttpClient httpClient,
        IOptions<FinnhubSettings> settings,
        ILogger<FinnhubNewsService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _rateLimiter = new SemaphoreSlim(1, 1);

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Add("X-Finnhub-Token", _settings.ApiKey);
    }

    public NewsSource Source => NewsSource.Finnhub;

    public async Task<IEnumerable<NewsArticle>> GetNewsAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        // Finnhub's crypto news endpoint returns general crypto news
        // We filter by date range after fetching
        var allNews = await FetchCryptoNewsAsync(startTime, endTime, cancellationToken);
        
        // Map symbol to Finnhub format (e.g., "BTC" -> "CRYPTO:BTC")
        var finnhubSymbol = MapToFinnhubSymbol(symbol);
        
        return allNews
            .Where(n => string.IsNullOrEmpty(finnhubSymbol) || 
                       n.Symbol.Contains(finnhubSymbol, StringComparison.OrdinalIgnoreCase) ||
                       n.Symbol == "CRYPTO")
            .Where(n => n.PublishedAt >= startTime && n.PublishedAt <= endTime);
    }

    public async Task<IEnumerable<NewsArticle>> GetLatestNewsAsync(
        string symbol,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var news = await FetchCryptoNewsAsync(
            DateTime.UtcNow.AddDays(-7), 
            DateTime.UtcNow, 
            cancellationToken);

        var finnhubSymbol = MapToFinnhubSymbol(symbol);

        return news
            .Where(n => string.IsNullOrEmpty(finnhubSymbol) || 
                       n.Symbol.Contains(finnhubSymbol, StringComparison.OrdinalIgnoreCase) ||
                       n.Symbol == "CRYPTO")
            .Take(limit);
    }

    public async Task<IEnumerable<NewsArticle>> GetGeneralCryptoNewsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var news = await FetchCryptoNewsAsync(
            DateTime.UtcNow.AddDays(-7), 
            DateTime.UtcNow, 
            cancellationToken);

        return news.Take(limit);
    }

    private async Task<IEnumerable<NewsArticle>> FetchCryptoNewsAsync(
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        await RespectRateLimitAsync(cancellationToken);

        try
        {
            var fromDate = startTime.ToString("yyyy-MM-dd");
            var toDate = endTime.ToString("yyyy-MM-dd");
            
            var url = $"news?category=crypto&from={fromDate}&to={toDate}";
            
            _logger.LogDebug("Fetching Finnhub crypto news: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var finnhubNews = JsonSerializer.Deserialize<List<FinnhubNewsItem>>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (finnhubNews == null || !finnhubNews.Any())
            {
                _logger.LogDebug("No news returned from Finnhub");
                return Enumerable.Empty<NewsArticle>();
            }

            _logger.LogInformation("Fetched {Count} news articles from Finnhub", finnhubNews.Count);

            return finnhubNews.Select(MapToNewsArticle);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch news from Finnhub");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Finnhub news response");
            throw;
        }
    }

    private NewsArticle MapToNewsArticle(FinnhubNewsItem item)
    {
        // Extract symbol from the "related" field (e.g., "CRYPTO:BTC" -> "BTC")
        var symbol = ExtractSymbol(item.Related);

        return new NewsArticle
        {
            ExternalId = item.Id.ToString(),
            Source = NewsSource.Finnhub,
            Symbol = symbol,
            Headline = item.Headline ?? string.Empty,
            Summary = item.Summary,
            Url = item.Url ?? string.Empty,
            ImageUrl = item.Image,
            Publisher = item.Source,
            PublishedAt = DateTimeOffset.FromUnixTimeSeconds(item.Datetime).UtcDateTime,
            Category = item.Category,
            RetrievedAt = DateTime.UtcNow,
            // Finnhub doesn't provide sentiment in the news endpoint
            SentimentScore = null,
            SentimentLabel = null,
            RelevanceScore = null
        };
    }

    private static string ExtractSymbol(string? related)
    {
        if (string.IsNullOrWhiteSpace(related))
            return "CRYPTO";

        // Handle format like "CRYPTO:BTC" or "BTC"
        if (related.Contains(':'))
        {
            var parts = related.Split(':');
            return parts.Length > 1 ? parts[1] : related;
        }

        return related;
    }

    private static string MapToFinnhubSymbol(string symbol)
    {
        // Map our symbol format (e.g., "BTCUSDT") to Finnhub format (e.g., "BTC")
        // For crypto pairs, we use the base asset
        return symbol.ToUpperInvariant() switch
        {
            "BTCUSDT" => "BTC",
            "ETHUSDT" => "ETH",
            "ETHBTC" => "ETH",
            var s when s.EndsWith("USDT") => s.Replace("USDT", ""),
            var s when s.EndsWith("BTC") => s.Replace("BTC", ""),
            _ => symbol
        };
    }

    private async Task RespectRateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var minInterval = TimeSpan.FromMinutes(1.0 / _settings.RateLimitPerMinute);

            if (timeSinceLastRequest < minInterval)
            {
                var delay = minInterval - timeSinceLastRequest;
                _logger.LogDebug("Rate limiting: waiting {Delay}ms", delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
}

/// <summary>
/// DTO for Finnhub news API response.
/// </summary>
internal class FinnhubNewsItem
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("datetime")]
    public long Datetime { get; set; }

    [JsonPropertyName("headline")]
    public string? Headline { get; set; }

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("related")]
    public string? Related { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
