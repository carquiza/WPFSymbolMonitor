using CryptoChart.Core.Enums;
using CryptoChart.Core.Models;
using CryptoChart.Data.Context;
using CryptoChart.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CryptoChart.Tests.Repositories;

public class CandleRepositoryTests
{
    private CryptoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CryptoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new CryptoDbContext(options);
        
        // Seed a test symbol
        context.Symbols.Add(new Symbol 
        { 
            Id = 1, 
            Name = "BTCUSDT", 
            BaseAsset = "BTC", 
            QuoteAsset = "USDT" 
        });
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task GetCandlesAsync_ReturnsCorrectDateRange()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new CandleRepository(context);

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = Enumerable.Range(0, 10).Select(i => new Candle
        {
            SymbolId = 1,
            TimeFrame = TimeFrame.Daily,
            OpenTime = baseTime.AddDays(i),
            CloseTime = baseTime.AddDays(i + 1).AddSeconds(-1),
            Open = 40000 + i * 100,
            High = 40500 + i * 100,
            Low = 39500 + i * 100,
            Close = 40200 + i * 100,
            Volume = 1000
        }).ToList();

        await repository.AddRangeAsync(candles);

        // Act
        var result = await repository.GetCandlesAsync(
            1, TimeFrame.Daily, 
            baseTime.AddDays(2), 
            baseTime.AddDays(5));

        // Assert
        Assert.Equal(4, result.Count());
        Assert.All(result, c => Assert.InRange(c.OpenTime, baseTime.AddDays(2), baseTime.AddDays(5)));
    }

    [Fact]
    public async Task GetLatestCandlesAsync_ReturnsInChronologicalOrder()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new CandleRepository(context);

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var candles = Enumerable.Range(0, 10).Select(i => new Candle
        {
            SymbolId = 1,
            TimeFrame = TimeFrame.Daily,
            OpenTime = baseTime.AddDays(i),
            CloseTime = baseTime.AddDays(i + 1).AddSeconds(-1),
            Open = 40000,
            High = 40500,
            Low = 39500,
            Close = 40200,
            Volume = 1000
        }).ToList();

        await repository.AddRangeAsync(candles);

        // Act
        var result = (await repository.GetLatestCandlesAsync(1, TimeFrame.Daily, 5)).ToList();

        // Assert
        Assert.Equal(5, result.Count);
        Assert.True(result.SequenceEqual(result.OrderBy(c => c.OpenTime)));
        Assert.Equal(baseTime.AddDays(5), result.First().OpenTime);
        Assert.Equal(baseTime.AddDays(9), result.Last().OpenTime);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingCandle()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new CandleRepository(context);

        var openTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var originalCandle = new Candle
        {
            SymbolId = 1,
            TimeFrame = TimeFrame.Daily,
            OpenTime = openTime,
            CloseTime = openTime.AddDays(1).AddSeconds(-1),
            Open = 40000,
            High = 40500,
            Low = 39500,
            Close = 40200,
            Volume = 1000
        };

        await repository.UpsertAsync(originalCandle);

        // Act
        var updatedCandle = new Candle
        {
            SymbolId = 1,
            TimeFrame = TimeFrame.Daily,
            OpenTime = openTime,
            CloseTime = openTime.AddDays(1).AddSeconds(-1),
            Open = 40000,
            High = 41000,  // Updated
            Low = 39000,   // Updated
            Close = 40800, // Updated
            Volume = 1500  // Updated
        };

        await repository.UpsertAsync(updatedCandle);

        // Assert
        var result = await repository.GetLatestCandleAsync(1, TimeFrame.Daily);
        Assert.NotNull(result);
        Assert.Equal(41000, result.High);
        Assert.Equal(39000, result.Low);
        Assert.Equal(40800, result.Close);
        Assert.Equal(1500, result.Volume);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new CandleRepository(context);

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dailyCandles = Enumerable.Range(0, 5).Select(i => new Candle
        {
            SymbolId = 1,
            TimeFrame = TimeFrame.Daily,
            OpenTime = baseTime.AddDays(i),
            CloseTime = baseTime.AddDays(i + 1).AddSeconds(-1),
            Open = 40000,
            High = 40500,
            Low = 39500,
            Close = 40200,
            Volume = 1000
        });

        var hourlyCandles = Enumerable.Range(0, 10).Select(i => new Candle
        {
            SymbolId = 1,
            TimeFrame = TimeFrame.Hourly,
            OpenTime = baseTime.AddHours(i),
            CloseTime = baseTime.AddHours(i + 1).AddSeconds(-1),
            Open = 40000,
            High = 40500,
            Low = 39500,
            Close = 40200,
            Volume = 1000
        });

        await repository.AddRangeAsync(dailyCandles);
        await repository.AddRangeAsync(hourlyCandles);

        // Act
        var dailyCount = await repository.GetCountAsync(1, TimeFrame.Daily);
        var hourlyCount = await repository.GetCountAsync(1, TimeFrame.Hourly);

        // Assert
        Assert.Equal(5, dailyCount);
        Assert.Equal(10, hourlyCount);
    }
}
