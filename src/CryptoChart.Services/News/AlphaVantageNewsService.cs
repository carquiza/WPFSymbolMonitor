using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptoChart.Services.News;

/// <summary>
/// Alpha Vantage API client for fetching cryptocurrency news with sentiment analysis.
/// API Documentation: https://www.alphavantage.co/documentation/#news-sentiment
/// </summary>
public class AlphaVantageNewsService : INewsService
{
    private readonly HttpClient _httpClient;
    private readonly AlphaVantageSettings _settings;
    private readonly ILogger<AlphaVantageNewsService> _logger;
    private readonly SemaphoreSlim _rateLimiter;
    private int _requestsToday;
    private DateTime _lastResetDate = DateTime.UtcNow.Date;

    public AlphaVantageNewsService(
        HttpClient httpClient,
        IOptions<AlphaVantageSettings> settings,
        ILogger<AlphaVantageNewsService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _rateLimiter = new SemaphoreSlim(1, 1);

        // BaseUrl should be "https://www.alphavantage.co" - we append "/query" in requests
        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/query"))
        {
            baseUrl = baseUrl[..^6]; // Remove "/query" suffix
        }
        _httpClient.BaseAddress = new Uri(baseUrl + "/");
    }

    public NewsSource Source => NewsSource.AlphaVantage;

    public async Task<IEnumerable<NewsArticle>> GetNewsAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        var ticker = MapToAlphaVantageTicker(symbol);
        var news = await FetchNewsSentimentAsync(ticker, startTime, endTime, cancellationToken);
        
        return news.Where(n => n.PublishedAt >= startTime && n.PublishedAt <= endTime);
    }

    public async Task<IEnumerable<NewsArticle>> GetLatestNewsAsync(
        string symbol,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var ticker = MapToAlphaVantageTicker(symbol);
        var news = await FetchNewsSentimentAsync(
            ticker, 
            null, 
            null, 
            cancellationToken, 
            limit);

        return news;
    }

    public async Task<IEnumerable<NewsArticle>> GetGeneralCryptoNewsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        // For general crypto news, use a common crypto ticker
        var news = await FetchNewsSentimentAsync(
            "CRYPTO:BTC", 
            null, 
            null, 
            cancellationToken, 
            limit);

        return news;
    }

    private async Task<IEnumerable<NewsArticle>> FetchNewsSentimentAsync(
        string ticker,
        DateTime? startTime,
        DateTime? endTime,
        CancellationToken cancellationToken,
        int? limit = null)
    {
        if (!await CheckRateLimitAsync(cancellationToken))
        {
            _logger.LogWarning("Alpha Vantage daily rate limit reached ({Limit} requests)", 
                _settings.RateLimitPerDay);
            return Enumerable.Empty<NewsArticle>();
        }

        try
        {
            var queryParams = new List<string>
            {
                "function=NEWS_SENTIMENT",
                $"tickers={ticker}",
                $"apikey={_settings.ApiKey}"
            };

            if (startTime.HasValue)
            {
                queryParams.Add($"time_from={startTime.Value:yyyyMMddTHHmm}");
            }

            if (endTime.HasValue)
            {
                queryParams.Add($"time_to={endTime.Value:yyyyMMddTHHmm}");
            }

            if (limit.HasValue)
            {
                queryParams.Add($"limit={Math.Min(limit.Value, 1000)}");
            }

            var url = $"query?{string.Join("&", queryParams)}";
            
            _logger.LogDebug("Fetching Alpha Vantage news sentiment: {Url}", 
                url.Replace(_settings.ApiKey, "***"));

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Check for API error messages
            if (json.Contains("\"Error Message\"") || json.Contains("\"Note\""))
            {
                _logger.LogWarning("Alpha Vantage returned an error or note: {Response}", 
                    json.Length > 500 ? json[..500] : json);
                return Enumerable.Empty<NewsArticle>();
            }

            var result = JsonSerializer.Deserialize<AlphaVantageNewsResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.Feed == null || !result.Feed.Any())
            {
                _logger.LogDebug("No news returned from Alpha Vantage");
                return Enumerable.Empty<NewsArticle>();
            }

            _logger.LogInformation("Fetched {Count} news articles from Alpha Vantage", result.Feed.Count);

            return result.Feed.Select(item => MapToNewsArticle(item, ticker));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch news from Alpha Vantage");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Alpha Vantage news response");
            throw;
        }
    }

    private NewsArticle MapToNewsArticle(AlphaVantageFeedItem item, string ticker)
    {
        // Find sentiment for our specific ticker
        var tickerSentiment = item.TickerSentiment?
            .FirstOrDefault(ts => ts.Ticker?.Equals(ticker, StringComparison.OrdinalIgnoreCase) == true);

        // Parse the published time (format: "20240101T120000")
        var publishedAt = ParseAlphaVantageDateTime(item.TimePublished);

        // Extract the base symbol from the ticker (e.g., "CRYPTO:BTC" -> "BTC")
        var symbol = ExtractSymbol(ticker);

        return new NewsArticle
        {
            ExternalId = GenerateExternalId(item),
            Source = NewsSource.AlphaVantage,
            Symbol = symbol,
            Headline = item.Title ?? string.Empty,
            Summary = TruncateSummary(item.Summary),
            Url = item.Url ?? string.Empty,
            ImageUrl = item.BannerImage,
            Publisher = item.Source,
            PublishedAt = publishedAt,
            Category = string.Join(", ", item.Topics?.Select(t => t.Topic) ?? Array.Empty<string>()),
            RetrievedAt = DateTime.UtcNow,
            SentimentScore = ParseDecimal(tickerSentiment?.TickerSentimentScore) 
                            ?? ParseDecimal(item.OverallSentimentScore?.ToString()),
            SentimentLabel = tickerSentiment?.TickerSentimentLabel 
                            ?? item.OverallSentimentLabel,
            RelevanceScore = ParseDecimal(tickerSentiment?.RelevanceScore)
        };
    }

    private static string ExtractSymbol(string ticker)
    {
        // Handle format like "CRYPTO:BTC" -> "BTC"
        if (ticker.Contains(':'))
        {
            var parts = ticker.Split(':');
            return parts.Length > 1 ? parts[1] : ticker;
        }
        return ticker;
    }

    private static string MapToAlphaVantageTicker(string symbol)
    {
        // Map our symbol format (e.g., "BTCUSDT") to Alpha Vantage format (e.g., "CRYPTO:BTC")
        return symbol.ToUpperInvariant() switch
        {
            "BTCUSDT" => "CRYPTO:BTC",
            "ETHUSDT" => "CRYPTO:ETH",
            "ETHBTC" => "CRYPTO:ETH",
            var s when s.EndsWith("USDT") => $"CRYPTO:{s.Replace("USDT", "")}",
            var s when s.EndsWith("BTC") => $"CRYPTO:{s.Replace("BTC", "")}",
            _ => $"CRYPTO:{symbol}"
        };
    }

    private static DateTime ParseAlphaVantageDateTime(string? dateTime)
    {
        if (string.IsNullOrWhiteSpace(dateTime))
            return DateTime.UtcNow;

        // Format: "20240101T120000"
        if (DateTime.TryParseExact(dateTime, "yyyyMMddTHHmmss", 
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
        {
            return result.ToUniversalTime();
        }

        // Alternative format without seconds
        if (DateTime.TryParseExact(dateTime, "yyyyMMddTHHmm", 
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
        {
            return result.ToUniversalTime();
        }

        return DateTime.UtcNow;
    }

    private static string GenerateExternalId(AlphaVantageFeedItem item)
    {
        // Alpha Vantage doesn't provide a unique ID, so we generate one from URL + timestamp
        var hash = HashCode.Combine(item.Url, item.TimePublished);
        return $"av_{Math.Abs(hash)}";
    }

    private static string? TruncateSummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return null;

        // Limit summary to ~2000 chars for database storage
        return summary.Length > 2000 ? summary[..1997] + "..." : summary;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) 
            ? result 
            : null;
    }

    private async Task<bool> CheckRateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            // Reset counter at midnight UTC
            var today = DateTime.UtcNow.Date;
            if (today > _lastResetDate)
            {
                _requestsToday = 0;
                _lastResetDate = today;
            }

            if (_requestsToday >= _settings.RateLimitPerDay)
            {
                return false;
            }

            _requestsToday++;
            return true;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }
}

#region Alpha Vantage DTOs

internal class AlphaVantageNewsResponse
{
    [JsonPropertyName("items")]
    public string? Items { get; set; }

    [JsonPropertyName("sentiment_score_definition")]
    public string? SentimentScoreDefinition { get; set; }

    [JsonPropertyName("relevance_score_definition")]
    public string? RelevanceScoreDefinition { get; set; }

    [JsonPropertyName("feed")]
    public List<AlphaVantageFeedItem>? Feed { get; set; }
}

internal class AlphaVantageFeedItem
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("time_published")]
    public string? TimePublished { get; set; }

    [JsonPropertyName("authors")]
    public List<string>? Authors { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("banner_image")]
    public string? BannerImage { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("category_within_source")]
    public string? CategoryWithinSource { get; set; }

    [JsonPropertyName("source_domain")]
    public string? SourceDomain { get; set; }

    [JsonPropertyName("topics")]
    public List<AlphaVantageTopic>? Topics { get; set; }

    [JsonPropertyName("overall_sentiment_score")]
    public decimal? OverallSentimentScore { get; set; }

    [JsonPropertyName("overall_sentiment_label")]
    public string? OverallSentimentLabel { get; set; }

    [JsonPropertyName("ticker_sentiment")]
    public List<AlphaVantageTickerSentiment>? TickerSentiment { get; set; }
}

internal class AlphaVantageTopic
{
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("relevance_score")]
    public string? RelevanceScore { get; set; }
}

internal class AlphaVantageTickerSentiment
{
    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("relevance_score")]
    public string? RelevanceScore { get; set; }

    [JsonPropertyName("ticker_sentiment_score")]
    public string? TickerSentimentScore { get; set; }

    [JsonPropertyName("ticker_sentiment_label")]
    public string? TickerSentimentLabel { get; set; }
}

#endregion
