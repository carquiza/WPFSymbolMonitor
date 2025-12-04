using CryptoChart.Core.Enums;
using CryptoChart.Core.Models;

namespace CryptoChart.Core.Interfaces;

/// <summary>
/// Repository interface for Symbol entities.
/// </summary>
public interface ISymbolRepository
{
    /// <summary>
    /// Gets all symbols.
    /// </summary>
    Task<IEnumerable<Symbol>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active symbols.
    /// </summary>
    Task<IEnumerable<Symbol>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a symbol by its name (e.g., "BTCUSDT").
    /// </summary>
    Task<Symbol?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a symbol by its ID.
    /// </summary>
    Task<Symbol?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new symbol.
    /// </summary>
    Task<Symbol> AddAsync(Symbol symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing symbol.
    /// </summary>
    Task UpdateAsync(Symbol symbol, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for Candle entities.
/// </summary>
public interface ICandleRepository
{
    /// <summary>
    /// Gets candles for a symbol and timeframe within a date range.
    /// </summary>
    Task<IEnumerable<Candle>> GetCandlesAsync(
        int symbolId,
        TimeFrame timeFrame,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent candles for a symbol and timeframe.
    /// </summary>
    Task<IEnumerable<Candle>> GetLatestCandlesAsync(
        int symbolId,
        TimeFrame timeFrame,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest candle for a symbol and timeframe.
    /// </summary>
    Task<Candle?> GetLatestCandleAsync(
        int symbolId,
        TimeFrame timeFrame,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the oldest candle for a symbol and timeframe.
    /// </summary>
    Task<Candle?> GetOldestCandleAsync(
        int symbolId,
        TimeFrame timeFrame,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a collection of candles (bulk insert).
    /// </summary>
    Task AddRangeAsync(IEnumerable<Candle> candles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a single candle (upsert based on SymbolId, TimeFrame, OpenTime).
    /// </summary>
    Task UpsertAsync(Candle candle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of candles for a symbol and timeframe.
    /// </summary>
    Task<int> GetCountAsync(
        int symbolId,
        TimeFrame timeFrame,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for NewsArticle entities.
/// </summary>
public interface INewsRepository
{
    /// <summary>
    /// Gets news articles for a symbol within a date range.
    /// </summary>
    Task<IEnumerable<Models.NewsArticle>> GetNewsAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets news articles that fall within a candle's time period.
    /// Useful for correlating news with specific candlesticks.
    /// </summary>
    Task<IEnumerable<Models.NewsArticle>> GetNewsForCandleAsync(
        string symbol,
        DateTime candleOpenTime,
        DateTime candleCloseTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent news articles for a symbol.
    /// </summary>
    Task<IEnumerable<Models.NewsArticle>> GetLatestNewsAsync(
        string symbol,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent news article for a symbol from a specific source.
    /// </summary>
    Task<Models.NewsArticle?> GetLatestAsync(
        string symbol,
        Enums.NewsSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a collection of news articles (bulk insert).
    /// </summary>
    Task AddRangeAsync(
        IEnumerable<Models.NewsArticle> articles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a news article already exists by its external ID and source.
    /// </summary>
    Task<bool> ExistsAsync(
        string externalId,
        Enums.NewsSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unique symbols that have news articles.
    /// </summary>
    Task<IEnumerable<string>> GetSymbolsWithNewsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of news articles for a symbol.
    /// </summary>
    Task<int> GetCountAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets news articles with a specific sentiment category.
    /// </summary>
    Task<IEnumerable<Models.NewsArticle>> GetBySentimentAsync(
        string symbol,
        bool? isBullish,
        int limit,
        CancellationToken cancellationToken = default);
}
