using System.Text.Json;
using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;

namespace CryptoChart.Services.Binance;

/// <summary>
/// Binance REST API client for fetching historical market data.
/// </summary>
public class BinanceMarketDataService : IMarketDataService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.binance.com";
    private const int MaxCandlesPerRequest = 1000;

    public BinanceMarketDataService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<IEnumerable<Candle>> GetHistoricalCandlesAsync(
        string symbol,
        TimeFrame timeFrame,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        var allCandles = new List<Candle>();
        var interval = timeFrame.ToBinanceInterval();
        var currentStart = startTime;

        while (currentStart < endTime)
        {
            var startMs = ToUnixMilliseconds(currentStart);
            var endMs = ToUnixMilliseconds(endTime);

            var url = $"/api/v3/klines?symbol={symbol}&interval={interval}&startTime={startMs}&endTime={endMs}&limit={MaxCandlesPerRequest}";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var klines = ParseKlineResponse(json);

            if (!klines.Any())
                break;

            var candles = klines.Select(k => MapToCandle(k, timeFrame)).ToList();
            allCandles.AddRange(candles);

            // Move start to after the last candle we received
            var lastCandle = candles.Last();
            currentStart = lastCandle.OpenTime.Add(timeFrame.GetCandleDuration());

            // Respect rate limits - brief delay between paginated requests
            if (currentStart < endTime)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        return allCandles;
    }

    public async Task<IEnumerable<Candle>> GetLatestCandlesAsync(
        string symbol,
        TimeFrame timeFrame,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var interval = timeFrame.ToBinanceInterval();
        var actualLimit = Math.Min(limit, MaxCandlesPerRequest);

        var url = $"/api/v3/klines?symbol={symbol}&interval={interval}&limit={actualLimit}";
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var klines = ParseKlineResponse(json);

        return klines.Select(k => MapToCandle(k, timeFrame));
    }

    private static List<BinanceKline> ParseKlineResponse(string json)
    {
        var klines = new List<BinanceKline>();
        
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        foreach (var element in root.EnumerateArray())
        {
            var values = element.EnumerateArray().ToList();
            
            klines.Add(new BinanceKline
            {
                OpenTime = values[0].GetInt64(),
                Open = decimal.Parse(values[1].GetString()!),
                High = decimal.Parse(values[2].GetString()!),
                Low = decimal.Parse(values[3].GetString()!),
                Close = decimal.Parse(values[4].GetString()!),
                Volume = decimal.Parse(values[5].GetString()!),
                CloseTime = values[6].GetInt64(),
                QuoteVolume = decimal.Parse(values[7].GetString()!),
                TradeCount = values[8].GetInt32(),
                TakerBuyBaseVolume = decimal.Parse(values[9].GetString()!),
                TakerBuyQuoteVolume = decimal.Parse(values[10].GetString()!)
            });
        }

        return klines;
    }

    private static Candle MapToCandle(BinanceKline kline, TimeFrame timeFrame)
    {
        return new Candle
        {
            TimeFrame = timeFrame,
            OpenTime = FromUnixMilliseconds(kline.OpenTime),
            CloseTime = FromUnixMilliseconds(kline.CloseTime),
            Open = kline.Open,
            High = kline.High,
            Low = kline.Low,
            Close = kline.Close,
            Volume = kline.Volume,
            QuoteVolume = kline.QuoteVolume,
            TradeCount = kline.TradeCount
        };
    }

    private static long ToUnixMilliseconds(DateTime dateTime)
    {
        return new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeMilliseconds();
    }

    private static DateTime FromUnixMilliseconds(long milliseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
    }
}
