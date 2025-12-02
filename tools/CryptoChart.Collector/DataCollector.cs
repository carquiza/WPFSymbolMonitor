using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using Serilog;

namespace CryptoChart.Collector;

/// <summary>
/// Service for collecting and storing cryptocurrency market data.
/// </summary>
public class DataCollector
{
    private readonly ISymbolRepository _symbolRepository;
    private readonly ICandleRepository _candleRepository;
    private readonly IMarketDataService _marketDataService;

    private static readonly TimeSpan BackfillDaily = TimeSpan.FromDays(365 * 5);    // 5 years
    private static readonly TimeSpan BackfillHourly = TimeSpan.FromDays(365);        // 1 year
    private static readonly TimeSpan CollectionInterval = TimeSpan.FromMinutes(5);

    public DataCollector(
        ISymbolRepository symbolRepository,
        ICandleRepository candleRepository,
        IMarketDataService marketDataService)
    {
        _symbolRepository = symbolRepository;
        _candleRepository = candleRepository;
        _marketDataService = marketDataService;
    }

    /// <summary>
    /// Fetches the latest candles for specified or all symbols.
    /// </summary>
    public async Task FetchLatestAsync(string? symbolName, string timeframeStr, CancellationToken ct)
    {
        var timeframe = ParseTimeFrame(timeframeStr);
        var symbols = await GetTargetSymbolsAsync(symbolName, ct);

        foreach (var symbol in symbols)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                Log.Information("Fetching latest {TimeFrame} candles for {Symbol}...", timeframe, symbol.Name);

                var candles = await _marketDataService.GetLatestCandlesAsync(
                    symbol.Name, timeframe, 100, ct);

                var candleList = candles.ToList();
                foreach (var candle in candleList)
                {
                    candle.SymbolId = symbol.Id;
                }

                await StoreCandlesAsync(candleList, ct);

                Log.Information("Stored {Count} candles for {Symbol}", candleList.Count, symbol.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching candles for {Symbol}", symbol.Name);
            }
        }
    }

    /// <summary>
    /// Performs historical backfill for specified or all symbols.
    /// </summary>
    public async Task BackfillAsync(string? symbolName, string timeframeStr, CancellationToken ct)
    {
        var timeframe = ParseTimeFrame(timeframeStr);
        var symbols = await GetTargetSymbolsAsync(symbolName, ct);
        var backfillDuration = timeframe == TimeFrame.Daily ? BackfillDaily : BackfillHourly;
        var endTime = DateTime.UtcNow;
        var startTime = endTime - backfillDuration;

        Log.Information("Starting backfill from {StartTime:yyyy-MM-dd} to {EndTime:yyyy-MM-dd}",
            startTime, endTime);

        foreach (var symbol in symbols)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await BackfillSymbolAsync(symbol, timeframe, startTime, endTime, ct);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during backfill for {Symbol}", symbol.Name);
            }
        }

        Log.Information("Backfill complete.");
    }

    private async Task BackfillSymbolAsync(
        Symbol symbol,
        TimeFrame timeframe,
        DateTime startTime,
        DateTime endTime,
        CancellationToken ct)
    {
        Log.Information("Backfilling {Symbol} ({TimeFrame}) from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd}...",
            symbol.Name, timeframe, startTime, endTime);

        // Check existing data to determine actual start
        var existingOldest = await _candleRepository.GetOldestCandleAsync(symbol.Id, timeframe, ct);
        var existingNewest = await _candleRepository.GetLatestCandleAsync(symbol.Id, timeframe, ct);

        var actualStartTime = startTime;
        var actualEndTime = endTime;

        if (existingOldest != null && existingNewest != null)
        {
            // Fill gaps: before existing data and after existing data
            if (existingOldest.OpenTime > startTime)
            {
                // Backfill before existing data
                await FetchAndStoreRangeAsync(symbol, timeframe, startTime, 
                    existingOldest.OpenTime.AddSeconds(-1), ct);
            }

            if (existingNewest.OpenTime < endTime)
            {
                // Fill after existing data
                await FetchAndStoreRangeAsync(symbol, timeframe,
                    existingNewest.OpenTime.Add(timeframe.GetCandleDuration()), endTime, ct);
            }
        }
        else
        {
            // No existing data, fetch everything
            await FetchAndStoreRangeAsync(symbol, timeframe, startTime, endTime, ct);
        }

        var finalCount = await _candleRepository.GetCountAsync(symbol.Id, timeframe, ct);
        Log.Information("Backfill complete for {Symbol}. Total candles: {Count}", symbol.Name, finalCount);
    }

    private async Task FetchAndStoreRangeAsync(
        Symbol symbol,
        TimeFrame timeframe,
        DateTime startTime,
        DateTime endTime,
        CancellationToken ct)
    {
        var totalExpected = (int)((endTime - startTime).TotalHours / 
            (timeframe == TimeFrame.Daily ? 24 : 1));
        var fetched = 0;

        Log.Information("Fetching ~{Expected} candles for {Symbol}...", totalExpected, symbol.Name);

        var candles = await _marketDataService.GetHistoricalCandlesAsync(
            symbol.Name, timeframe, startTime, endTime, ct);

        var candleList = candles.ToList();
        foreach (var candle in candleList)
        {
            candle.SymbolId = symbol.Id;
        }

        fetched = candleList.Count;
        await StoreCandlesAsync(candleList, ct);

        Log.Information("Fetched and stored {Fetched}/{Expected} candles for {Symbol}",
            fetched, totalExpected, symbol.Name);
    }

    /// <summary>
    /// Runs in continuous mode, periodically fetching new data.
    /// </summary>
    public async Task RunContinuousAsync(string? symbolName, string timeframeStr, CancellationToken ct)
    {
        var timeframe = ParseTimeFrame(timeframeStr);

        Log.Information("Starting continuous collection mode (interval: {Interval})", CollectionInterval);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await FetchLatestAsync(symbolName, timeframeStr, ct);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in continuous collection cycle");
            }

            Log.Information("Next collection in {Interval}...", CollectionInterval);

            try
            {
                await Task.Delay(CollectionInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<IEnumerable<Symbol>> GetTargetSymbolsAsync(string? symbolName, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(symbolName))
        {
            var symbol = await _symbolRepository.GetByNameAsync(symbolName, ct);
            if (symbol == null)
            {
                Log.Warning("Symbol {Symbol} not found in database", symbolName);
                return Enumerable.Empty<Symbol>();
            }
            return new[] { symbol };
        }

        return await _symbolRepository.GetActiveAsync(ct);
    }

    private async Task StoreCandlesAsync(IEnumerable<Candle> candles, CancellationToken ct)
    {
        var candleList = candles.ToList();
        if (candleList.Count == 0) return;

        // Use upsert for each candle to handle duplicates gracefully
        foreach (var candle in candleList)
        {
            await _candleRepository.UpsertAsync(candle, ct);
        }
    }

    private static TimeFrame ParseTimeFrame(string timeframeStr)
    {
        return timeframeStr.ToLowerInvariant() switch
        {
            "1h" or "hourly" => TimeFrame.Hourly,
            "1d" or "daily" => TimeFrame.Daily,
            _ => throw new ArgumentException($"Invalid timeframe: {timeframeStr}. Use '1h' or '1d'.")
        };
    }
}
