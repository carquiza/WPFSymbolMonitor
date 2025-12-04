using CryptoChart.Core.Enums;

namespace CryptoChart.Core.Models;

/// <summary>
/// Represents a news article related to cryptocurrency markets.
/// Used for correlating news events with market activity.
/// </summary>
public class NewsArticle
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// External ID from the source API (for deduplication).
    /// </summary>
    public required string ExternalId { get; set; }

    /// <summary>
    /// The source of this news article.
    /// </summary>
    public NewsSource Source { get; set; }

    /// <summary>
    /// Related cryptocurrency symbol (e.g., "BTCUSDT", "ETHUSDT").
    /// For general crypto news, this may be "CRYPTO" or the primary related symbol.
    /// </summary>
    public required string Symbol { get; set; }

    /// <summary>
    /// The headline/title of the article.
    /// </summary>
    public required string Headline { get; set; }

    /// <summary>
    /// Summary or excerpt of the article.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// URL to the full article.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// URL to the article's thumbnail or featured image.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Original source/publisher name (e.g., "Bloomberg", "CoinDesk").
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// When the article was published (UTC).
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Sentiment score from the API (-1.0 to 1.0, where negative is bearish, positive is bullish).
    /// Null if sentiment analysis is not available.
    /// </summary>
    public decimal? SentimentScore { get; set; }

    /// <summary>
    /// Human-readable sentiment label (e.g., "Bullish", "Bearish", "Neutral", "Somewhat-Bullish").
    /// </summary>
    public string? SentimentLabel { get; set; }

    /// <summary>
    /// Relevance score indicating how relevant the article is to the symbol (0.0 to 1.0).
    /// </summary>
    public decimal? RelevanceScore { get; set; }

    /// <summary>
    /// Category of the news (e.g., "crypto", "technology", "general").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// When this article was retrieved from the API (UTC).
    /// </summary>
    public DateTime RetrievedAt { get; set; }

    /// <summary>
    /// Gets whether the sentiment is bullish (positive score or bullish label).
    /// </summary>
    public bool IsBullish => SentimentScore > 0.1m || 
        (SentimentLabel?.Contains("Bullish", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>
    /// Gets whether the sentiment is bearish (negative score or bearish label).
    /// </summary>
    public bool IsBearish => SentimentScore < -0.1m || 
        (SentimentLabel?.Contains("Bearish", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>
    /// Gets whether the sentiment is neutral.
    /// </summary>
    public bool IsNeutral => !IsBullish && !IsBearish;

    /// <summary>
    /// Gets a normalized sentiment category for display purposes.
    /// </summary>
    public string SentimentCategory
    {
        get
        {
            if (IsBullish) return "Bullish";
            if (IsBearish) return "Bearish";
            return "Neutral";
        }
    }
}
