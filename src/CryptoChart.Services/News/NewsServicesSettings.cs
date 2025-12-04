namespace CryptoChart.Services.News;

/// <summary>
/// Configuration settings for all news services.
/// </summary>
public class NewsServicesSettings
{
    public const string SectionName = "NewsServices";

    /// <summary>
    /// Finnhub API settings.
    /// </summary>
    public FinnhubSettings Finnhub { get; set; } = new();

    /// <summary>
    /// Alpha Vantage API settings.
    /// </summary>
    public AlphaVantageSettings AlphaVantage { get; set; } = new();
}

/// <summary>
/// Finnhub API configuration.
/// </summary>
public class FinnhubSettings
{
    /// <summary>
    /// Finnhub API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Finnhub base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://finnhub.io/api/v1";

    /// <summary>
    /// Rate limit: requests per minute (free tier = 60).
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 60;

    /// <summary>
    /// Whether the service is enabled.
    /// </summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Alpha Vantage API configuration.
/// </summary>
public class AlphaVantageSettings
{
    /// <summary>
    /// Alpha Vantage API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Alpha Vantage base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.alphavantage.co/query";

    /// <summary>
    /// Rate limit: requests per day (free tier = 25 for NEWS_SENTIMENT).
    /// </summary>
    public int RateLimitPerDay { get; set; } = 25;

    /// <summary>
    /// Whether the service is enabled.
    /// </summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
