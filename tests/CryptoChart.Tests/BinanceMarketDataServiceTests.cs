using System.Net;
using CryptoChart.Core.Enums;
using CryptoChart.Services.Binance;
using Moq;
using Moq.Protected;

namespace CryptoChart.Tests.Services;

public class BinanceMarketDataServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly BinanceMarketDataService _service;

    public BinanceMarketDataServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.binance.com")
        };
        _service = new BinanceMarketDataService(_httpClient);
    }

    [Fact]
    public async Task GetLatestCandlesAsync_ParsesResponseCorrectly()
    {
        // Arrange
        var jsonResponse = @"[
            [1609459200000,""29000.00"",""29500.00"",""28800.00"",""29300.00"",""1000.00"",1609545599999,""29000000.00"",500,""500.00"",""14500000.00"",""0""],
            [1609545600000,""29300.00"",""30000.00"",""29100.00"",""29800.00"",""1200.00"",1609631999999,""35000000.00"",600,""600.00"",""17500000.00"",""0""]
        ]";

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        // Act
        var result = (await _service.GetLatestCandlesAsync("BTCUSDT", TimeFrame.Daily, 2)).ToList();

        // Assert
        Assert.Equal(2, result.Count);

        var firstCandle = result[0];
        Assert.Equal(29000.00m, firstCandle.Open);
        Assert.Equal(29500.00m, firstCandle.High);
        Assert.Equal(28800.00m, firstCandle.Low);
        Assert.Equal(29300.00m, firstCandle.Close);
        Assert.Equal(1000.00m, firstCandle.Volume);
        Assert.Equal(TimeFrame.Daily, firstCandle.TimeFrame);
    }

    [Fact]
    public async Task GetLatestCandlesAsync_SetsCorrectTimeFrameOnCandles()
    {
        // Arrange
        var jsonResponse = @"[
            [1609459200000,""29000.00"",""29500.00"",""28800.00"",""29300.00"",""1000.00"",1609545599999,""29000000.00"",500,""500.00"",""14500000.00"",""0""]
        ]";

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        // Act
        var hourlyResult = (await _service.GetLatestCandlesAsync("BTCUSDT", TimeFrame.Hourly, 1)).First();

        // Assert
        Assert.Equal(TimeFrame.Hourly, hourlyResult.TimeFrame);
    }

    [Fact]
    public async Task GetLatestCandlesAsync_ConvertsTimestampsCorrectly()
    {
        // Arrange - Using known timestamp: 2021-01-01 00:00:00 UTC
        var timestamp = 1609459200000L;
        var expectedTime = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var jsonResponse = $@"[
            [{timestamp},""29000.00"",""29500.00"",""28800.00"",""29300.00"",""1000.00"",{timestamp + 86399999},""29000000.00"",500,""500.00"",""14500000.00"",""0""]
        ]";

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        // Act
        var result = (await _service.GetLatestCandlesAsync("BTCUSDT", TimeFrame.Daily, 1)).First();

        // Assert
        Assert.Equal(expectedTime, result.OpenTime);
    }

    [Fact]
    public async Task GetLatestCandlesAsync_LimitsTo1000()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri!.Query.Contains("limit=1000")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]")
            });

        // Act
        await _service.GetLatestCandlesAsync("BTCUSDT", TimeFrame.Daily, 2000);

        // Assert - Verify the request was made with limit=1000
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.Query.Contains("limit=1000")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetHistoricalCandlesAsync_HandlesEmptyResponse()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]")
            });

        // Act
        var result = await _service.GetHistoricalCandlesAsync(
            "BTCUSDT",
            TimeFrame.Daily,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLatestCandlesAsync_ThrowsOnHttpError()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Bad Request")
            });

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _service.GetLatestCandlesAsync("INVALIDPAIR", TimeFrame.Daily, 10));
    }
}
