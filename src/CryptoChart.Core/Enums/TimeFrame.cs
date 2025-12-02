namespace CryptoChart.Core.Enums;

/// <summary>
/// Supported chart timeframes for candle data.
/// </summary>
public enum TimeFrame
{
    /// <summary>
    /// 1-hour candles
    /// </summary>
    Hourly,
    
    /// <summary>
    /// Daily candles
    /// </summary>
    Daily
}

/// <summary>
/// Extension methods for TimeFrame enum.
/// </summary>
public static class TimeFrameExtensions
{
    /// <summary>
    /// Gets the Binance API interval string for this timeframe.
    /// </summary>
    public static string ToBinanceInterval(this TimeFrame timeFrame) => timeFrame switch
    {
        TimeFrame.Hourly => "1h",
        TimeFrame.Daily => "1d",
        _ => throw new ArgumentOutOfRangeException(nameof(timeFrame))
    };

    /// <summary>
    /// Gets the display name for this timeframe.
    /// </summary>
    public static string ToDisplayString(this TimeFrame timeFrame) => timeFrame switch
    {
        TimeFrame.Hourly => "1 Hour",
        TimeFrame.Daily => "1 Day",
        _ => throw new ArgumentOutOfRangeException(nameof(timeFrame))
    };

    /// <summary>
    /// Gets the duration of one candle in this timeframe.
    /// </summary>
    public static TimeSpan GetCandleDuration(this TimeFrame timeFrame) => timeFrame switch
    {
        TimeFrame.Hourly => TimeSpan.FromHours(1),
        TimeFrame.Daily => TimeSpan.FromDays(1),
        _ => throw new ArgumentOutOfRangeException(nameof(timeFrame))
    };
}
