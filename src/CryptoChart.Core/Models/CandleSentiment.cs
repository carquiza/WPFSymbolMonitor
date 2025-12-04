namespace CryptoChart.Core.Models;

/// <summary>
/// Aggregated sentiment data for a specific candle/time period.
/// Used for visualizing news sentiment alongside price data.
/// </summary>
public class CandleSentiment
{
    /// <summary>
    /// The candle's open time (used to correlate with price data).
    /// </summary>
    public DateTime OpenTime { get; set; }

    /// <summary>
    /// The candle's close time.
    /// </summary>
    public DateTime CloseTime { get; set; }

    /// <summary>
    /// Number of bullish news articles in this time period.
    /// </summary>
    public int BullishCount { get; set; }

    /// <summary>
    /// Number of bearish news articles in this time period.
    /// </summary>
    public int BearishCount { get; set; }

    /// <summary>
    /// Number of neutral news articles in this time period.
    /// </summary>
    public int NeutralCount { get; set; }

    /// <summary>
    /// Total number of news articles in this time period.
    /// </summary>
    public int TotalCount => BullishCount + BearishCount + NeutralCount;

    /// <summary>
    /// Net sentiment (-1 to +1 range based on article counts).
    /// Positive = more bullish, Negative = more bearish.
    /// </summary>
    public double NetSentiment
    {
        get
        {
            if (TotalCount == 0) return 0;
            return (BullishCount - BearishCount) / (double)TotalCount;
        }
    }

    /// <summary>
    /// Gets whether this period has any news.
    /// </summary>
    public bool HasNews => TotalCount > 0;

    /// <summary>
    /// List of article IDs for drill-down capability.
    /// </summary>
    public List<long> ArticleIds { get; set; } = new();
}
