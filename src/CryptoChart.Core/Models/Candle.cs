using CryptoChart.Core.Enums;

namespace CryptoChart.Core.Models;

/// <summary>
/// Represents an OHLCV candlestick data point.
/// </summary>
public class Candle
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to Symbol.
    /// </summary>
    public int SymbolId { get; set; }

    /// <summary>
    /// Navigation property to Symbol.
    /// </summary>
    public Symbol? Symbol { get; set; }

    /// <summary>
    /// The timeframe of this candle.
    /// </summary>
    public TimeFrame TimeFrame { get; set; }

    /// <summary>
    /// Candle open time (UTC).
    /// </summary>
    public DateTime OpenTime { get; set; }

    /// <summary>
    /// Candle close time (UTC).
    /// </summary>
    public DateTime CloseTime { get; set; }

    /// <summary>
    /// Opening price.
    /// </summary>
    public decimal Open { get; set; }

    /// <summary>
    /// Highest price during the period.
    /// </summary>
    public decimal High { get; set; }

    /// <summary>
    /// Lowest price during the period.
    /// </summary>
    public decimal Low { get; set; }

    /// <summary>
    /// Closing price.
    /// </summary>
    public decimal Close { get; set; }

    /// <summary>
    /// Trading volume in base asset.
    /// </summary>
    public decimal Volume { get; set; }

    /// <summary>
    /// Trading volume in quote asset.
    /// </summary>
    public decimal QuoteVolume { get; set; }

    /// <summary>
    /// Number of trades during this period.
    /// </summary>
    public int TradeCount { get; set; }

    /// <summary>
    /// Whether this is a bullish (green) candle.
    /// </summary>
    public bool IsBullish => Close >= Open;

    /// <summary>
    /// Whether this is a bearish (red) candle.
    /// </summary>
    public bool IsBearish => Close < Open;

    /// <summary>
    /// The body size of the candle (absolute difference between open and close).
    /// </summary>
    public decimal BodySize => Math.Abs(Close - Open);

    /// <summary>
    /// The full range of the candle (high - low).
    /// </summary>
    public decimal Range => High - Low;

    /// <summary>
    /// The upper wick size.
    /// </summary>
    public decimal UpperWick => High - Math.Max(Open, Close);

    /// <summary>
    /// The lower wick size.
    /// </summary>
    public decimal LowerWick => Math.Min(Open, Close) - Low;
}
