using CryptoChart.Core.Enums;
using CryptoChart.Core.Models;

namespace CryptoChart.Core.Interfaces;

/// <summary>
/// Service interface for fetching historical candle data from an exchange.
/// </summary>
public interface IMarketDataService
{
    /// <summary>
    /// Fetches historical candles from the exchange.
    /// </summary>
    /// <param name="symbol">The trading pair symbol (e.g., "BTCUSDT").</param>
    /// <param name="timeFrame">The candle timeframe.</param>
    /// <param name="startTime">Start of the date range (UTC).</param>
    /// <param name="endTime">End of the date range (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of candles.</returns>
    Task<IEnumerable<Candle>> GetHistoricalCandlesAsync(
        string symbol,
        TimeFrame timeFrame,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the latest candles from the exchange.
    /// </summary>
    /// <param name="symbol">The trading pair symbol.</param>
    /// <param name="timeFrame">The candle timeframe.</param>
    /// <param name="limit">Maximum number of candles to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of candles.</returns>
    Task<IEnumerable<Candle>> GetLatestCandlesAsync(
        string symbol,
        TimeFrame timeFrame,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for real-time market data streaming.
/// </summary>
public interface IRealtimeMarketService : IAsyncDisposable
{
    /// <summary>
    /// Event raised when a new candle update is received.
    /// </summary>
    event EventHandler<CandleUpdateEventArgs>? CandleUpdated;

    /// <summary>
    /// Event raised when the connection status changes.
    /// </summary>
    event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;

    /// <summary>
    /// Subscribes to real-time candle updates for a symbol and timeframe.
    /// </summary>
    Task SubscribeAsync(string symbol, TimeFrame timeFrame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from real-time candle updates for a symbol and timeframe.
    /// </summary>
    Task UnsubscribeAsync(string symbol, TimeFrame timeFrame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from all real-time updates.
    /// </summary>
    Task UnsubscribeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the service is currently connected.
    /// </summary>
    bool IsConnected { get; }
}

/// <summary>
/// Event arguments for candle updates.
/// </summary>
public class CandleUpdateEventArgs : EventArgs
{
    public required string Symbol { get; init; }
    public required TimeFrame TimeFrame { get; init; }
    public required Candle Candle { get; init; }
    public required bool IsClosed { get; init; }
}

/// <summary>
/// Event arguments for connection status changes.
/// </summary>
public class ConnectionStatusEventArgs : EventArgs
{
    public required bool IsConnected { get; init; }
    public string? Message { get; init; }
    public Exception? Error { get; init; }
}
