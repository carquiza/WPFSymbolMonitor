using System.Net;
using System.Text.Json;
using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using CryptoChart.Data.Context;
using CryptoChart.Data.Repositories;
using CryptoChart.Services.News;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace CryptoChart.Tests;

public class NewsServiceTests
{
    #region FinnhubNewsService Tests

    [Fact]
    public async Task FinnhubNewsService_ParsesResponseCorrectly()
    {
        // Arrange
        var mockResponse = @"[
            {
                ""category"": ""crypto"",
                ""datetime"": 1700000000,
                ""headline"": ""Bitcoin Reaches New High"",
                ""id"": 12345,
                ""image"": ""https://example.com/image.jpg"",
                ""related"": ""CRYPTO:BTC"",
                ""source"": ""CoinDesk"",
                ""summary"": ""Bitcoin has reached a new all-time high today."",
                ""url"": ""https://example.com/article""
            }
        ]";

        var httpClient = CreateMockHttpClient(mockResponse);
        var settings = Options.Create(new FinnhubSettings 
        { 
            ApiKey = "test-key",
            BaseUrl = "https://finnhub.io/api/v1"
        });
        var logger = Mock.Of<ILogger<FinnhubNewsService>>();

        var service = new FinnhubNewsService(httpClient, settings, logger);

        // Act
        var news = await service.GetGeneralCryptoNewsAsync(10);

        // Assert
        var article = news.First();
        Assert.Equal("12345", article.ExternalId);
        Assert.Equal(NewsSource.Finnhub, article.Source);
        Assert.Equal("BTC", article.Symbol);
        Assert.Equal("Bitcoin Reaches New High", article.Headline);
        Assert.Equal("CoinDesk", article.Publisher);
        Assert.Equal("https://example.com/article", article.Url);
    }

    [Fact]
    public async Task FinnhubNewsService_HandlesEmptyResponse()
    {
        // Arrange
        var httpClient = CreateMockHttpClient("[]");
        var settings = Options.Create(new FinnhubSettings 
        { 
            ApiKey = "test-key",
            BaseUrl = "https://finnhub.io/api/v1"
        });
        var logger = Mock.Of<ILogger<FinnhubNewsService>>();

        var service = new FinnhubNewsService(httpClient, settings, logger);

        // Act
        var news = await service.GetGeneralCryptoNewsAsync(10);

        // Assert
        Assert.Empty(news);
    }

    [Fact]
    public void FinnhubNewsService_HasCorrectSource()
    {
        // Arrange
        var httpClient = CreateMockHttpClient("[]");
        var settings = Options.Create(new FinnhubSettings 
        { 
            ApiKey = "test-key",
            BaseUrl = "https://finnhub.io/api/v1"
        });
        var logger = Mock.Of<ILogger<FinnhubNewsService>>();

        var service = new FinnhubNewsService(httpClient, settings, logger);

        // Assert
        Assert.Equal(NewsSource.Finnhub, service.Source);
    }

    #endregion

    #region AlphaVantageNewsService Tests

    [Fact]
    public async Task AlphaVantageNewsService_ParsesResponseCorrectly()
    {
        // Arrange
        var mockResponse = @"{
            ""feed"": [
                {
                    ""title"": ""ETH Price Analysis"",
                    ""url"": ""https://example.com/eth-article"",
                    ""time_published"": ""20231114T120000"",
                    ""summary"": ""Ethereum shows bullish momentum."",
                    ""source"": ""CryptoNews"",
                    ""overall_sentiment_score"": 0.25,
                    ""overall_sentiment_label"": ""Bullish"",
                    ""ticker_sentiment"": [
                        {
                            ""ticker"": ""CRYPTO:ETH"",
                            ""relevance_score"": ""0.95"",
                            ""ticker_sentiment_score"": ""0.30"",
                            ""ticker_sentiment_label"": ""Bullish""
                        }
                    ]
                }
            ]
        }";

        var httpClient = CreateMockHttpClient(mockResponse);
        var settings = Options.Create(new AlphaVantageSettings 
        { 
            ApiKey = "test-key",
            BaseUrl = "https://www.alphavantage.co/query"
        });
        var logger = Mock.Of<ILogger<AlphaVantageNewsService>>();

        var service = new AlphaVantageNewsService(httpClient, settings, logger);

        // Act
        var news = await service.GetLatestNewsAsync("ETHUSDT", 10);

        // Assert
        var article = news.First();
        Assert.Equal(NewsSource.AlphaVantage, article.Source);
        Assert.Equal("ETH", article.Symbol);
        Assert.Equal("ETH Price Analysis", article.Headline);
        Assert.Equal("CryptoNews", article.Publisher);
        Assert.Equal(0.30m, article.SentimentScore);
        Assert.Equal("Bullish", article.SentimentLabel);
        Assert.True(article.IsBullish);
    }

    [Fact]
    public async Task AlphaVantageNewsService_HandlesMissingSentiment()
    {
        // Arrange
        var mockResponse = @"{
            ""feed"": [
                {
                    ""title"": ""Crypto Market Update"",
                    ""url"": ""https://example.com/update"",
                    ""time_published"": ""20231114T100000"",
                    ""summary"": ""General market update."",
                    ""source"": ""MarketWatch""
                }
            ]
        }";

        var httpClient = CreateMockHttpClient(mockResponse);
        var settings = Options.Create(new AlphaVantageSettings 
        { 
            ApiKey = "test-key",
            BaseUrl = "https://www.alphavantage.co/query"
        });
        var logger = Mock.Of<ILogger<AlphaVantageNewsService>>();

        var service = new AlphaVantageNewsService(httpClient, settings, logger);

        // Act
        var news = await service.GetGeneralCryptoNewsAsync(10);

        // Assert
        var article = news.First();
        Assert.Null(article.SentimentScore);
        Assert.Null(article.SentimentLabel);
        Assert.False(article.IsBullish);
        Assert.False(article.IsBearish);
        Assert.True(article.IsNeutral);
    }

    #endregion

    #region NewsRepository Tests

    [Fact]
    public async Task NewsRepository_AddRangeAsync_PreventsDirectDuplicates()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NewsRepository(context);

        var articles = new List<NewsArticle>
        {
            CreateTestArticle("1", "BTC", "Headline 1"),
            CreateTestArticle("2", "BTC", "Headline 2"),
        };

        // Act
        await repository.AddRangeAsync(articles);
        
        // Try to add same articles again
        await repository.AddRangeAsync(articles);

        // Assert
        var count = await context.NewsArticles.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task NewsRepository_GetNewsAsync_FiltersByDateRange()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NewsRepository(context);

        var now = DateTime.UtcNow;
        var articles = new List<NewsArticle>
        {
            CreateTestArticle("1", "BTC", "Old", now.AddDays(-10)),
            CreateTestArticle("2", "BTC", "Recent", now.AddDays(-2)),
            CreateTestArticle("3", "BTC", "Today", now),
        };
        await repository.AddRangeAsync(articles);

        // Act
        var result = await repository.GetNewsAsync("BTC", now.AddDays(-5), now);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, a => Assert.True(a.PublishedAt >= now.AddDays(-5)));
    }

    [Fact]
    public async Task NewsRepository_GetNewsForCandleAsync_ReturnsCorrectArticles()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NewsRepository(context);

        var candleOpen = new DateTime(2023, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var candleClose = new DateTime(2023, 11, 14, 13, 0, 0, DateTimeKind.Utc);

        var articles = new List<NewsArticle>
        {
            CreateTestArticle("1", "BTC", "Before", candleOpen.AddHours(-1)),
            CreateTestArticle("2", "BTC", "During", candleOpen.AddMinutes(30)),
            CreateTestArticle("3", "BTC", "After", candleClose.AddHours(1)),
        };
        await repository.AddRangeAsync(articles);

        // Act
        var result = await repository.GetNewsForCandleAsync("BTC", candleOpen, candleClose);

        // Assert
        Assert.Single(result);
        Assert.Equal("During", result.First().Headline);
    }

    [Fact]
    public async Task NewsRepository_ExistsAsync_DetectsDuplicates()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NewsRepository(context);

        var article = CreateTestArticle("unique-id-123", "ETH", "Test Article");
        await repository.AddRangeAsync(new[] { article });

        // Act & Assert
        Assert.True(await repository.ExistsAsync("unique-id-123", NewsSource.Finnhub));
        Assert.False(await repository.ExistsAsync("different-id", NewsSource.Finnhub));
        Assert.False(await repository.ExistsAsync("unique-id-123", NewsSource.AlphaVantage));
    }

    #endregion

    #region NewsArticle Model Tests

    [Theory]
    [InlineData(0.5, null, true, false)]
    [InlineData(-0.5, null, false, true)]
    [InlineData(0.05, null, false, false)]
    [InlineData(null, "Bullish", true, false)]
    [InlineData(null, "Bearish", false, true)]
    [InlineData(null, "Somewhat-Bullish", true, false)]
    [InlineData(null, "Neutral", false, false)]
    public void NewsArticle_SentimentProperties_CalculateCorrectly(
        double? scoreDouble, string? label, bool expectedBullish, bool expectedBearish)
    {
        decimal? score = scoreDouble.HasValue ? (decimal)scoreDouble.Value : null;

        // Arrange
        var article = new NewsArticle
        {
            ExternalId = "1",
            Symbol = "BTC",
            Headline = "Test",
            Url = "https://example.com",
            SentimentScore = score,
            SentimentLabel = label
        };

        // Assert
        Assert.Equal(expectedBullish, article.IsBullish);
        Assert.Equal(expectedBearish, article.IsBearish);
        Assert.Equal(!expectedBullish && !expectedBearish, article.IsNeutral);
    }

    #endregion

    #region AggregatedNewsService Tests

    [Fact]
    public async Task AggregatedNewsService_CombinesMultipleSources()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NewsRepository(context);
        var logger = Mock.Of<ILogger<AggregatedNewsService>>();

        var mockService1 = new Mock<INewsService>();
        mockService1.Setup(s => s.Source).Returns(NewsSource.Finnhub);
        mockService1.Setup(s => s.GetLatestNewsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTestArticle("f1", "BTC", "Finnhub News") });

        var mockService2 = new Mock<INewsService>();
        mockService2.Setup(s => s.Source).Returns(NewsSource.AlphaVantage);
        mockService2.Setup(s => s.GetLatestNewsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTestArticle("av1", "BTC", "AlphaVantage News", source: NewsSource.AlphaVantage) });

        var services = new List<INewsService> { mockService1.Object, mockService2.Object };
        var aggregator = new AggregatedNewsService(services, repository, logger);

        // Act
        var news = await aggregator.GetLatestNewsAsync("BTCUSDT", 10, forceRefresh: true);

        // Assert
        Assert.Equal(2, news.Count());
    }

    [Fact]
    public async Task AggregatedNewsService_DeduplicatesByUrl()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new NewsRepository(context);
        var logger = Mock.Of<ILogger<AggregatedNewsService>>();

        var sameUrl = "https://example.com/same-article";
        var mockService1 = new Mock<INewsService>();
        mockService1.Setup(s => s.Source).Returns(NewsSource.Finnhub);
        mockService1.Setup(s => s.GetLatestNewsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTestArticle("f1", "BTC", "Same Article", url: sameUrl) });

        var mockService2 = new Mock<INewsService>();
        mockService2.Setup(s => s.Source).Returns(NewsSource.AlphaVantage);
        mockService2.Setup(s => s.GetLatestNewsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTestArticle("av1", "BTC", "Same Article", source: NewsSource.AlphaVantage, url: sameUrl) });

        var services = new List<INewsService> { mockService1.Object, mockService2.Object };
        var aggregator = new AggregatedNewsService(services, repository, logger);

        // Act
        var news = await aggregator.GetLatestNewsAsync("BTCUSDT", 10, forceRefresh: true);

        // Assert - should deduplicate by URL
        Assert.Single(news);
    }

    #endregion

    #region CryptoSymbolMapper Tests

    [Theory]
    [InlineData("BTCUSDT", "BTC")]
    [InlineData("ETHUSDT", "ETH")]
    [InlineData("ETHBTC", "ETH")]
    [InlineData("SOLUSDT", "SOL")]
    [InlineData("BTC", "BTC")]
    public void CryptoSymbolMapper_GetBaseAsset_ReturnsCorrectSymbol(string input, string expected)
    {
        Assert.Equal(expected, CryptoSymbolMapper.GetBaseAsset(input));
    }

    [Theory]
    [InlineData("BTCUSDT", "CRYPTO:BTC")]
    [InlineData("ETHUSDT", "CRYPTO:ETH")]
    [InlineData("SOLUSDT", "CRYPTO:SOL")]
    public void CryptoSymbolMapper_ToAlphaVantageFormat_ReturnsCorrectFormat(string input, string expected)
    {
        Assert.Equal(expected, CryptoSymbolMapper.ToAlphaVantageFormat(input));
    }

    [Theory]
    [InlineData("BTCUSDT", "Bitcoin")]
    [InlineData("BTC", "Bitcoin")]
    [InlineData("ETHUSDT", "Ethereum")]
    [InlineData("UNKNOWN", null)]
    public void CryptoSymbolMapper_GetFullName_ReturnsCorrectName(string input, string? expected)
    {
        Assert.Equal(expected, CryptoSymbolMapper.GetFullName(input));
    }

    #endregion

    #region Helper Methods

    private static HttpClient CreateMockHttpClient(string responseContent)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        return new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.example.com")
        };
    }

    private static CryptoDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CryptoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new CryptoDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static NewsArticle CreateTestArticle(
        string externalId, 
        string symbol, 
        string headline, 
        DateTime? publishedAt = null,
        NewsSource source = NewsSource.Finnhub,
        string? url = null)
    {
        return new NewsArticle
        {
            ExternalId = externalId,
            Source = source,
            Symbol = symbol,
            Headline = headline,
            Url = url ?? $"https://example.com/{externalId}",
            PublishedAt = publishedAt ?? DateTime.UtcNow,
            RetrievedAt = DateTime.UtcNow
        };
    }

    #endregion
}
