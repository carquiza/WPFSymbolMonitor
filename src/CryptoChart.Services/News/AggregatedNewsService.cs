using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using Microsoft.Extensions.Logging;

namespace CryptoChart.Services.News;

/// <summary>
/// Aggregates news from multiple sources (Finnhub and Alpha Vantage),
/// handles caching via repository, and deduplicates results.
/// </summary>
public class AggregatedNewsService
{
    private readonly IEnumerable<INewsService> _newsServices;
    private readonly INewsRepository _newsRepository;
    private readonly ILogger<AggregatedNewsService> _logger;

    public AggregatedNewsService(
        IEnumerable<INewsService> newsServices,
        INewsRepository newsRepository,
        ILogger<AggregatedNewsService> logger)
    {
        _newsServices = newsServices;
        _newsRepository = newsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets news for a symbol within a date range.
    /// First checks the database cache, then fetches from APIs if needed.
    /// </summary>
    public async Task<IEnumerable<NewsArticle>> GetNewsAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        // Check cache first unless forced refresh
        if (!forceRefresh)
        {
            var cachedNews = await _newsRepository.GetNewsAsync(
                MapSymbolToStorage(symbol), startTime, endTime, cancellationToken);
            
            if (cachedNews.Any())
            {
                _logger.LogDebug("Returning {Count} cached news articles for {Symbol}", 
                    cachedNews.Count(), symbol);
                return cachedNews;
            }
        }

        // Fetch from all available sources
        var allNews = new List<NewsArticle>();
        
        foreach (var service in _newsServices)
        {
            try
            {
                var news = await service.GetNewsAsync(symbol, startTime, endTime, cancellationToken);
                allNews.AddRange(news);
                
                _logger.LogDebug("Fetched {Count} articles from {Source}", 
                    news.Count(), service.Source);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch news from {Source}", service.Source);
                // Continue with other sources
            }
        }

        // Update symbol to our storage format
        foreach (var article in allNews)
        {
            article.Symbol = MapSymbolToStorage(symbol);
        }

        // Deduplicate and save to cache
        var deduplicated = DeduplicateNews(allNews);
        
        if (deduplicated.Any())
        {
            await _newsRepository.AddRangeAsync(deduplicated, cancellationToken);
            _logger.LogInformation("Saved {Count} new articles to database", deduplicated.Count());
        }

        // Return all news (including previously cached)
        return await _newsRepository.GetNewsAsync(
            MapSymbolToStorage(symbol), startTime, endTime, cancellationToken);
    }

    /// <summary>
    /// Gets the latest news for a symbol.
    /// </summary>
    public async Task<IEnumerable<NewsArticle>> GetLatestNewsAsync(
        string symbol,
        int limit = 50,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var storageSymbol = MapSymbolToStorage(symbol);
        
        // Check if we need to refresh
        if (!forceRefresh)
        {
            var cachedNews = await _newsRepository.GetLatestNewsAsync(
                storageSymbol, limit, cancellationToken);
            
            if (cachedNews.Any())
            {
                // Check if cache is fresh (last article within 1 hour)
                var latest = cachedNews.First();
                if (DateTime.UtcNow - latest.RetrievedAt < TimeSpan.FromHours(1))
                {
                    _logger.LogDebug("Returning {Count} cached news articles", cachedNews.Count());
                    return cachedNews;
                }
            }
        }

        // Fetch fresh news from APIs
        var allNews = new List<NewsArticle>();
        
        foreach (var service in _newsServices)
        {
            try
            {
                var news = await service.GetLatestNewsAsync(symbol, limit, cancellationToken);
                allNews.AddRange(news);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch latest news from {Source}", service.Source);
            }
        }

        // Update symbol and save
        foreach (var article in allNews)
        {
            article.Symbol = storageSymbol;
        }

        var deduplicated = DeduplicateNews(allNews);
        
        if (deduplicated.Any())
        {
            await _newsRepository.AddRangeAsync(deduplicated, cancellationToken);
        }

        return await _newsRepository.GetLatestNewsAsync(storageSymbol, limit, cancellationToken);
    }

    /// <summary>
    /// Gets general cryptocurrency news from all sources.
    /// </summary>
    public async Task<IEnumerable<NewsArticle>> GetGeneralCryptoNewsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var allNews = new List<NewsArticle>();
        
        foreach (var service in _newsServices)
        {
            try
            {
                var news = await service.GetGeneralCryptoNewsAsync(limit, cancellationToken);
                allNews.AddRange(news);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch general news from {Source}", service.Source);
            }
        }

        // Deduplicate and save
        var deduplicated = DeduplicateNews(allNews);
        
        if (deduplicated.Any())
        {
            await _newsRepository.AddRangeAsync(deduplicated, cancellationToken);
        }

        return deduplicated.Take(limit);
    }

    /// <summary>
    /// Gets news articles that fall within a specific candle's time period.
    /// Useful for displaying news markers on the chart.
    /// </summary>
    public async Task<IEnumerable<NewsArticle>> GetNewsForCandleAsync(
        string symbol,
        DateTime candleOpenTime,
        DateTime candleCloseTime,
        CancellationToken cancellationToken = default)
    {
        return await _newsRepository.GetNewsForCandleAsync(
            MapSymbolToStorage(symbol), 
            candleOpenTime, 
            candleCloseTime, 
            cancellationToken);
    }

    /// <summary>
    /// Gets aggregated sentiment for a time period.
    /// </summary>
    public async Task<NewsSentimentSummary> GetSentimentSummaryAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        var news = await _newsRepository.GetNewsAsync(
            MapSymbolToStorage(symbol), startTime, endTime, cancellationToken);

        var articles = news.ToList();
        var withSentiment = articles.Where(a => a.SentimentScore.HasValue).ToList();

        return new NewsSentimentSummary
        {
            Symbol = symbol,
            StartTime = startTime,
            EndTime = endTime,
            TotalArticles = articles.Count,
            ArticlesWithSentiment = withSentiment.Count,
            BullishCount = articles.Count(a => a.IsBullish),
            BearishCount = articles.Count(a => a.IsBearish),
            NeutralCount = articles.Count(a => a.IsNeutral),
            AverageSentiment = withSentiment.Any() 
                ? withSentiment.Average(a => a.SentimentScore!.Value) 
                : null
        };
    }

    /// <summary>
    /// Maps various symbol formats to our storage format.
    /// </summary>
    private static string MapSymbolToStorage(string symbol)
    {
        // For crypto pairs, extract the base asset
        return symbol.ToUpperInvariant() switch
        {
            "BTCUSDT" => "BTC",
            "ETHUSDT" => "ETH",
            "ETHBTC" => "ETH",
            var s when s.EndsWith("USDT") => s.Replace("USDT", ""),
            var s when s.EndsWith("BTC") => s.Replace("BTC", ""),
            _ => symbol.ToUpperInvariant()
        };
    }

    /// <summary>
    /// Deduplicates news articles by URL or similar headlines.
    /// </summary>
    private static List<NewsArticle> DeduplicateNews(IEnumerable<NewsArticle> articles)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NewsArticle>();

        foreach (var article in articles.OrderByDescending(a => a.PublishedAt))
        {
            // Use URL as primary deduplication key
            var key = article.Url;
            
            if (!seen.Contains(key))
            {
                seen.Add(key);
                result.Add(article);
            }
        }

        return result;
    }
}

/// <summary>
/// Summary of news sentiment for a time period.
/// </summary>
public class NewsSentimentSummary
{
    public required string Symbol { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int TotalArticles { get; init; }
    public int ArticlesWithSentiment { get; init; }
    public int BullishCount { get; init; }
    public int BearishCount { get; init; }
    public int NeutralCount { get; init; }
    public decimal? AverageSentiment { get; init; }

    /// <summary>
    /// Gets the overall sentiment category based on average sentiment.
    /// </summary>
    public string OverallSentiment
    {
        get
        {
            if (!AverageSentiment.HasValue)
                return "Unknown";
            
            return AverageSentiment.Value switch
            {
                > 0.1m => "Bullish",
                < -0.1m => "Bearish",
                _ => "Neutral"
            };
        }
    }

    /// <summary>
    /// Gets the dominant sentiment based on article counts.
    /// </summary>
    public string DominantSentiment
    {
        get
        {
            if (BullishCount > BearishCount && BullishCount > NeutralCount)
                return "Bullish";
            if (BearishCount > BullishCount && BearishCount > NeutralCount)
                return "Bearish";
            return "Neutral";
        }
    }
}
