using System.Text.Json.Serialization;

namespace CryptoChart.Services.Binance;

/// <summary>
/// Binance Kline (candlestick) data from REST API.
/// The REST API returns an array of arrays, so we parse manually.
/// </summary>
public class BinanceKline
{
    public long OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public long CloseTime { get; set; }
    public decimal QuoteVolume { get; set; }
    public int TradeCount { get; set; }
    public decimal TakerBuyBaseVolume { get; set; }
    public decimal TakerBuyQuoteVolume { get; set; }
}

/// <summary>
/// WebSocket Kline stream event from Binance.
/// </summary>
public class BinanceKlineStreamEvent
{
    [JsonPropertyName("e")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("E")]
    public long EventTime { get; set; }

    [JsonPropertyName("s")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("k")]
    public BinanceKlineData Kline { get; set; } = null!;
}

/// <summary>
/// Kline data from WebSocket stream.
/// </summary>
public class BinanceKlineData
{
    [JsonPropertyName("t")]
    public long OpenTime { get; set; }

    [JsonPropertyName("T")]
    public long CloseTime { get; set; }

    [JsonPropertyName("s")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("i")]
    public string Interval { get; set; } = string.Empty;

    [JsonPropertyName("f")]
    public long FirstTradeId { get; set; }

    [JsonPropertyName("L")]
    public long LastTradeId { get; set; }

    [JsonPropertyName("o")]
    public string Open { get; set; } = string.Empty;

    [JsonPropertyName("c")]
    public string Close { get; set; } = string.Empty;

    [JsonPropertyName("h")]
    public string High { get; set; } = string.Empty;

    [JsonPropertyName("l")]
    public string Low { get; set; } = string.Empty;

    [JsonPropertyName("v")]
    public string Volume { get; set; } = string.Empty;

    [JsonPropertyName("n")]
    public int TradeCount { get; set; }

    [JsonPropertyName("x")]
    public bool IsClosed { get; set; }

    [JsonPropertyName("q")]
    public string QuoteVolume { get; set; } = string.Empty;

    [JsonPropertyName("V")]
    public string TakerBuyBaseVolume { get; set; } = string.Empty;

    [JsonPropertyName("Q")]
    public string TakerBuyQuoteVolume { get; set; } = string.Empty;
}
