using CryptoChart.Core.Enums;
using CryptoChart.Core.Models;

namespace CryptoChart.Tests.Models;

public class CandleTests
{
    [Fact]
    public void IsBullish_ReturnsTrueWhenCloseGreaterOrEqualToOpen()
    {
        var candle = new Candle
        {
            Open = 100,
            High = 110,
            Low = 95,
            Close = 105
        };

        Assert.True(candle.IsBullish);
        Assert.False(candle.IsBearish);
    }

    [Fact]
    public void IsBullish_ReturnsTrueWhenCloseEqualsOpen()
    {
        var candle = new Candle
        {
            Open = 100,
            High = 110,
            Low = 95,
            Close = 100 // Equal to open = doji but still bullish by our definition
        };

        Assert.True(candle.IsBullish);
        Assert.False(candle.IsBearish);
    }

    [Fact]
    public void IsBearish_ReturnsTrueWhenCloseLessThanOpen()
    {
        var candle = new Candle
        {
            Open = 100,
            High = 105,
            Low = 90,
            Close = 92
        };

        Assert.True(candle.IsBearish);
        Assert.False(candle.IsBullish);
    }

    [Fact]
    public void BodySize_CalculatesCorrectly()
    {
        var candle = new Candle
        {
            Open = 100,
            High = 110,
            Low = 90,
            Close = 95
        };

        Assert.Equal(5, candle.BodySize); // |100 - 95| = 5
    }

    [Fact]
    public void Range_CalculatesCorrectly()
    {
        var candle = new Candle
        {
            Open = 100,
            High = 110,
            Low = 90,
            Close = 95
        };

        Assert.Equal(20, candle.Range); // 110 - 90 = 20
    }

    [Fact]
    public void UpperWick_CalculatesCorrectlyForBullishCandle()
    {
        var candle = new Candle
        {
            Open = 100,
            High = 110,
            Low = 95,
            Close = 105
        };

        // Upper wick = High - Max(Open, Close) = 110 - 105 = 5
        Assert.Equal(5, candle.UpperWick);
    }

    [Fact]
    public void UpperWick_CalculatesCorrectlyForBearishCandle()
    {
        var candle = new Candle
        {
            Open = 105,
            High = 110,
            Low = 95,
            Close = 100
        };

        // Upper wick = High - Max(Open, Close) = 110 - 105 = 5
        Assert.Equal(5, candle.UpperWick);
    }

    [Fact]
    public void LowerWick_CalculatesCorrectlyForBullishCandle()
    {
        var candle = new Candle
        {
            Open = 100,
            High = 110,
            Low = 95,
            Close = 105
        };

        // Lower wick = Min(Open, Close) - Low = 100 - 95 = 5
        Assert.Equal(5, candle.LowerWick);
    }

    [Fact]
    public void LowerWick_CalculatesCorrectlyForBearishCandle()
    {
        var candle = new Candle
        {
            Open = 105,
            High = 110,
            Low = 92,
            Close = 100
        };

        // Lower wick = Min(Open, Close) - Low = 100 - 92 = 8
        Assert.Equal(8, candle.LowerWick);
    }
}

public class SymbolTests
{
    [Fact]
    public void DisplayName_FormatsCorrectly()
    {
        var symbol = new Symbol
        {
            Name = "BTCUSDT",
            BaseAsset = "BTC",
            QuoteAsset = "USDT"
        };

        Assert.Equal("BTC/USDT", symbol.DisplayName);
    }

    [Fact]
    public void FromBinanceSymbol_CreatesCorrectly()
    {
        var symbol = Symbol.FromBinanceSymbol("ETHBTC", "ETH", "BTC");

        Assert.Equal("ETHBTC", symbol.Name);
        Assert.Equal("ETH", symbol.BaseAsset);
        Assert.Equal("BTC", symbol.QuoteAsset);
        Assert.Equal("ETH/BTC", symbol.DisplayName);
    }

    [Fact]
    public void IsActive_DefaultsToTrue()
    {
        var symbol = new Symbol
        {
            Name = "BTCUSDT",
            BaseAsset = "BTC",
            QuoteAsset = "USDT"
        };

        Assert.True(symbol.IsActive);
    }
    [Fact]
    public void ToString_ReturnsDisplayName()
    {
        var symbol = new Symbol
        {
            Name = "BTCUSDT",
            BaseAsset = "BTC",
            QuoteAsset = "USDT"
        };

        Assert.Equal("BTC/USDT", symbol.ToString());
    }
}

public class TimeFrameExtensionsTests
{
    [Theory]
    [InlineData(TimeFrame.Hourly, "1h")]
    [InlineData(TimeFrame.Daily, "1d")]
    public void ToBinanceInterval_ReturnsCorrectString(TimeFrame timeFrame, string expected)
    {
        Assert.Equal(expected, timeFrame.ToBinanceInterval());
    }

    [Theory]
    [InlineData(TimeFrame.Hourly, "1 Hour")]
    [InlineData(TimeFrame.Daily, "1 Day")]
    public void ToDisplayString_ReturnsCorrectString(TimeFrame timeFrame, string expected)
    {
        Assert.Equal(expected, timeFrame.ToDisplayString());
    }

    [Fact]
    public void GetCandleDuration_ReturnsCorrectDuration()
    {
        Assert.Equal(TimeSpan.FromHours(1), TimeFrame.Hourly.GetCandleDuration());
        Assert.Equal(TimeSpan.FromDays(1), TimeFrame.Daily.GetCandleDuration());
    }
}
