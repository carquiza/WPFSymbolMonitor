using CryptoChart.Core.Interfaces;
using CryptoChart.Services.News;
using Serilog;

namespace CryptoChart.Collector;

/// <summary>
/// Service for collecting and storing cryptocurrency news articles.
/// </summary>
public class NewsCollector
{
    private readonly AggregatedNewsService _newsService;
    private readonly INewsRepository _newsRepository;
    private readonly ISymbolRepository _symbolRepository;

    private static readonly TimeSpan NewsBackfillDuration = TimeSpan.FromDays(30);  // 30 days of historical news
    private static readonly TimeSpan NewsCollectionInterval = TimeSpan.FromMinutes(30);

    public NewsCollector(
        AggregatedNewsService newsService,
        INewsRepository newsRepository,
        ISymbolRepository symbolRepository)
    {
        _newsService = newsService;
        _newsRepository = newsRepository;
        _symbolRepository = symbolRepository;
    }

    /// <summary>
    /// Fetches the latest news for specified or all symbols.
    /// </summary>
    public async Task FetchLatestNewsAsync(string? symbolName, CancellationToken ct)
    {
        var symbols = await GetTargetSymbolsAsync(symbolName, ct);

        foreach (var symbol in symbols)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                Log.Information("Fetching latest news for {Symbol}...", symbol);

                var news = await _newsService.GetLatestNewsAsync(
                    symbol, 
                    limit: 50, 
                    forceRefresh: true, 
                    cancellationToken: ct);

                var newsList = news.ToList();
                Log.Information("Retrieved {Count} news articles for {Symbol}", newsList.Count, symbol);

                // Log sentiment summary
                var bullish = newsList.Count(n => n.IsBullish);
                var bearish = newsList.Count(n => n.IsBearish);
                var neutral = newsList.Count(n => n.IsNeutral);
                
                Log.Information("Sentiment breakdown for {Symbol}: Bullish={Bullish}, Bearish={Bearish}, Neutral={Neutral}",
                    symbol, bullish, bearish, neutral);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching news for {Symbol}", symbol);
            }
        }
    }

    /// <summary>
    /// Performs historical news backfill for specified or all symbols.
    /// </summary>
    public async Task BackfillNewsAsync(string? symbolName, int? days, CancellationToken ct)
    {
        var symbols = await GetTargetSymbolsAsync(symbolName, ct);
        var backfillDuration = days.HasValue 
            ? TimeSpan.FromDays(days.Value) 
            : NewsBackfillDuration;
        
        var endTime = DateTime.UtcNow;
        var startTime = endTime - backfillDuration;

        Log.Information("Starting news backfill from {StartTime:yyyy-MM-dd} to {EndTime:yyyy-MM-dd}",
            startTime, endTime);

        foreach (var symbol in symbols)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await BackfillSymbolNewsAsync(symbol, startTime, endTime, ct);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during news backfill for {Symbol}", symbol);
            }
        }

        Log.Information("News backfill complete.");
    }

    private async Task BackfillSymbolNewsAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        CancellationToken ct)
    {
        Log.Information("Backfilling news for {Symbol} from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}...",
            symbol, startTime, endTime);

        // Check existing news count before backfill
        var existingCount = await _newsRepository.GetCountAsync(
            CryptoSymbolMapper.GetBaseAsset(symbol), ct);

        Log.Information("Existing news articles for {Symbol}: {Count}", symbol, existingCount);

        // Fetch news for the date range
        var news = await _newsService.GetNewsAsync(
            symbol, 
            startTime, 
            endTime, 
            forceRefresh: true, 
            cancellationToken: ct);

        var newsList = news.ToList();

        // Get final count
        var finalCount = await _newsRepository.GetCountAsync(
            CryptoSymbolMapper.GetBaseAsset(symbol), ct);

        var newArticles = finalCount - existingCount;

        Log.Information("Backfill complete for {Symbol}. New articles: {New}, Total: {Total}",
            symbol, newArticles, finalCount);

        // Log sentiment summary for backfilled news
        if (newsList.Any())
        {
            var sentimentSummary = await _newsService.GetSentimentSummaryAsync(
                symbol, startTime, endTime, ct);

            Log.Information(
                "Sentiment summary for {Symbol} ({Start:MMM dd} - {End:MMM dd}): " +
                "Total={Total}, Bullish={Bullish}, Bearish={Bearish}, Neutral={Neutral}, Avg={Avg:F3}",
                symbol, startTime, endTime,
                sentimentSummary.TotalArticles,
                sentimentSummary.BullishCount,
                sentimentSummary.BearishCount,
                sentimentSummary.NeutralCount,
                sentimentSummary.AverageSentiment ?? 0);
        }
    }

    /// <summary>
    /// Runs in continuous mode, periodically fetching news.
    /// </summary>
    public async Task RunContinuousNewsAsync(string? symbolName, CancellationToken ct)
    {
        Log.Information("Starting continuous news collection mode (interval: {Interval})", 
            NewsCollectionInterval);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await FetchLatestNewsAsync(symbolName, ct);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in continuous news collection cycle");
            }

            Log.Information("Next news collection in {Interval}...", NewsCollectionInterval);

            try
            {
                await Task.Delay(NewsCollectionInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Fetches general crypto news (not symbol-specific).
    /// </summary>
    public async Task FetchGeneralNewsAsync(int limit, CancellationToken ct)
    {
        try
        {
            Log.Information("Fetching general cryptocurrency news...");

            var news = await _newsService.GetGeneralCryptoNewsAsync(limit, ct);
            var newsList = news.ToList();

            Log.Information("Retrieved {Count} general crypto news articles", newsList.Count);

            // Show top headlines
            foreach (var article in newsList.Take(5))
            {
                var sentiment = article.SentimentCategory;
                var source = article.Publisher ?? "Unknown";
                Log.Information("[{Sentiment}] {Source}: {Headline}",
                    sentiment, source, article.Headline);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching general crypto news");
        }
    }

    /// <summary>
    /// Shows news statistics for stored articles.
    /// </summary>
    public async Task ShowNewsStatsAsync(CancellationToken ct)
    {
        Log.Information("=== News Database Statistics ===");

        var symbols = await _newsRepository.GetSymbolsWithNewsAsync(ct);
        var symbolList = symbols.ToList();

        if (!symbolList.Any())
        {
            Log.Information("No news articles in database.");
            return;
        }

        Log.Information("Symbols with news: {Symbols}", string.Join(", ", symbolList));

        foreach (var symbol in symbolList)
        {
            var count = await _newsRepository.GetCountAsync(symbol, ct);
            var latest = await _newsRepository.GetLatestNewsAsync(symbol, 1, ct);
            var latestArticle = latest.FirstOrDefault();

            Log.Information("{Symbol}: {Count} articles, Latest: {Latest:yyyy-MM-dd HH:mm}",
                symbol, count, latestArticle?.PublishedAt);
        }
    }

    private async Task<IEnumerable<string>> GetTargetSymbolsAsync(string? symbolName, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(symbolName))
        {
            return new[] { symbolName.ToUpperInvariant() };
        }

        // Get all active symbols from database
        var activeSymbols = await _symbolRepository.GetActiveAsync(ct);
        return activeSymbols.Select(s => s.Name);
    }
}
