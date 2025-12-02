namespace CryptoChart.Core.Models;

/// <summary>
/// Represents a trading pair symbol (e.g., BTCUSDT).
/// </summary>
public class Symbol
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The symbol name as used by the exchange (e.g., "BTCUSDT").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The base asset (e.g., "BTC" in BTCUSDT).
    /// </summary>
    public required string BaseAsset { get; set; }

    /// <summary>
    /// The quote asset (e.g., "USDT" in BTCUSDT).
    /// </summary>
    public required string QuoteAsset { get; set; }

    /// <summary>
    /// Display name for UI (e.g., "BTC/USDT").
    /// </summary>
    public string DisplayName => $"{BaseAsset}/{QuoteAsset}";

    /// <summary>
    /// Whether this symbol is active and should be tracked.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for candles.
    /// </summary>
    public ICollection<Candle> Candles { get; set; } = new List<Candle>();

    /// <summary>
    /// Creates a Symbol from the standard Binance symbol format.
    /// </summary>
    public static Symbol FromBinanceSymbol(string binanceSymbol, string baseAsset, string quoteAsset)
    {
        return new Symbol
        {
            Name = binanceSymbol,
            BaseAsset = baseAsset,
            QuoteAsset = quoteAsset
        };
    }
}
