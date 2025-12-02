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
