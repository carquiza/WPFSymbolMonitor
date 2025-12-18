# CryptoChartWPF

A sophisticated cryptocurrency charting application built with WPF and .NET 8, demonstrating advanced fintech UI development skills. Features custom-rendered OHLC candlestick charts, real-time Binance WebSocket integration, and news sentiment analysis.

> **Portfolio Project**: This application showcases custom WPF control development, high-performance rendering, reactive programming patterns, and clean architecture—all built without third-party charting libraries.

## Features

- **Custom Candlestick Charts** - Hand-coded OHLCV visualization using `DrawingVisual` for high-performance rendering
- **Real-Time Data** - WebSocket integration with Binance for live price updates
- **Pan & Zoom** - Smooth chart navigation with mouse wheel zoom and drag-to-pan
- **Interactive Crosshair** - Real-time price/time display with hover tooltips
- **News Sentiment Analysis** - Integrated news from Finnhub and Alpha Vantage with sentiment scoring
- **Click-to-Pin Selection** - Click candles to pin them for detailed news analysis
- **Dark Theme** - Professional trading platform aesthetic
- **Data Persistence** - SQLite database for offline access to historical data

## Screenshots

*Coming soon*

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8, WPF |
| Architecture | MVVM with CommunityToolkit.Mvvm |
| Database | SQLite with Entity Framework Core |
| Real-time | Binance WebSocket API |
| News APIs | Finnhub, Alpha Vantage |
| Reactive | System.Reactive (Rx.NET) |
| Logging | Serilog |
| CLI | System.CommandLine |

## Prerequisites

### Required
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 (WPF requirement)

### Optional (for news features)
- [Finnhub API Key](https://finnhub.io/register) - Free tier: 60 requests/minute
- [Alpha Vantage API Key](https://www.alphavantage.co/support/#api-key) - Free tier: 25 requests/day

## Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/CryptoChartWPF.git
   cd CryptoChartWPF
   ```

2. **Build the solution**
   ```bash
   dotnet build CryptoChartWPF.sln
   ```

3. **Configure API keys** (optional, for news features)
   
   Using User Secrets (recommended):
   ```bash
   cd src/CryptoChart.App
   dotnet user-secrets init
   dotnet user-secrets set "NewsServices:Finnhub:ApiKey" "YOUR_FINNHUB_KEY"
   dotnet user-secrets set "NewsServices:AlphaVantage:ApiKey" "YOUR_ALPHAVANTAGE_KEY"
   ```
   
   Or edit `tools/CryptoChart.Collector/appsettings.json` directly.

4. **Run the application**
   ```bash
   dotnet run --project src/CryptoChart.App
   ```

## Usage

### WPF Application

Launch the application to view cryptocurrency charts:

- **Symbol Selection** - Choose from BTCUSDT, ETHUSDT, or ETHBTC
- **Timeframe** - Switch between Hourly (1H) and Daily (1D) views
- **Navigation** - Scroll wheel to zoom, drag to pan
- **Hover** - Mouse over candles to see OHLCV data and news sentiment
- **Click** - Click a candle to pin it; the news panel will show that candle's articles even when you mouse away

### Data Collection

On first run, the app fetches data from Binance if the database is empty. For larger historical datasets, use the Collector tool.

---

## CryptoChart Collector Tool

A command-line utility for collecting and managing cryptocurrency market data and news.

### Basic Usage

```bash
cd tools/CryptoChart.Collector
dotnet run -- [options]
```

Or after building:
```bash
CryptoChart.Collector.exe [options]
```

### Command Line Options

#### Market Data Options

| Option | Description |
|--------|-------------|
| `--symbol <SYMBOL>` | Target symbol (e.g., `BTCUSDT`). If omitted, collects all active symbols. |
| `--timeframe <1h\|1d>` | Candle timeframe. Default: `1d` |
| `--backfill` | Perform historical backfill (5 years daily, 1 year hourly) |
| `--continuous` | Run continuously, fetching new data every 5 minutes |

#### News Options

| Option | Description |
|--------|-------------|
| `--collect-news` | Fetch latest news articles for crypto symbols |
| `--news-backfill` | Perform historical news backfill |
| `--news-days <N>` | Number of days to backfill news. Default: 30 |
| `--news-stats` | Display news database statistics |
| `--general-news` | Fetch general crypto news (not symbol-specific) |

### Examples

**Backfill historical market data (all symbols):**
```bash
dotnet run -- --backfill
```
This fetches 5 years of daily candles and 1 year of hourly candles for BTCUSDT, ETHUSDT, and ETHBTC.

**Backfill specific symbol and timeframe:**
```bash
dotnet run -- --backfill --symbol BTCUSDT --timeframe 1h
```

**Fetch latest candles:**
```bash
dotnet run -- --symbol ETHUSDT --timeframe 1d
```

**Run continuous market data collection:**
```bash
dotnet run -- --continuous --timeframe 1h
```

**Fetch latest news:**
```bash
dotnet run -- --collect-news
```

**Backfill 60 days of news:**
```bash
dotnet run -- --news-backfill --news-days 60
```

**Run continuous news collection:**
```bash
dotnet run -- --collect-news --continuous
```

**View news statistics:**
```bash
dotnet run -- --news-stats
```

**Combine operations:**
```bash
dotnet run -- --backfill --news-backfill --news-days 30
```

### Output & Logs

- Console output shows progress with timestamps
- Log files are written to `logs/collector-YYYYMMDD.log`
- Press `Ctrl+C` to gracefully stop continuous operations

---

## API Limitations (Free Tiers)

### Binance API

| Aspect | Limit |
|--------|-------|
| Rate Limit | 1,200 requests/minute (IP-based) |
| Historical Data | Full history available |
| Authentication | Not required for public endpoints |
| Backfill | **No limitations** - full 5+ years of data available |

The Binance public API provides complete historical OHLCV data with generous rate limits. The collector includes a 100ms delay between paginated requests to stay well within limits.

### Finnhub API (Free Tier)

| Aspect | Limit |
|--------|-------|
| Rate Limit | **60 requests/minute** |
| Data Scope | General crypto news (not symbol-specific filtering at API level) |
| Historical Range | ~7 days of news available |
| Sentiment | Not included in free tier |

**Limitations:**
- News is category-based ("crypto"), not specific to individual coins
- Limited historical depth (~1 week)
- No sentiment scoring in free tier

### Alpha Vantage API (Free Tier)

| Aspect | Limit |
|--------|-------|
| Rate Limit | **25 requests/day** for NEWS_SENTIMENT |
| Data Scope | Ticker-specific news with sentiment |
| Historical Range | Variable, typically 1-3 months |
| Sentiment | ✅ Included (score + label per ticker) |

**Limitations:**
- **Severely limited** - only 25 API calls per day
- Best used sparingly for sentiment analysis
- News backfill will exhaust daily quota quickly
- Consider upgrading for production use ($49.99/month for 500 calls/day)

### Recommendations

1. **For market data**: Binance free tier is more than sufficient for any use case
2. **For news**: Use Finnhub as primary source (60/min is generous), Alpha Vantage for sentiment-enriched queries
3. **For backfill**: Run market data backfill freely; limit news backfill to conserve Alpha Vantage quota
4. **Daily workflow**: Collect news once or twice daily to stay within Alpha Vantage limits

---

## Database

Data is stored in SQLite at:
```
%LocalApplicationData%\CryptoChart\cryptodata.db
```

Typically: `C:\Users\<YourName>\AppData\Local\CryptoChart\cryptodata.db`

### Tables

| Table | Description |
|-------|-------------|
| `Symbols` | Trading pairs (BTCUSDT, ETHUSDT, ETHBTC) |
| `Candles` | OHLCV candlestick data with timestamps |
| `NewsArticles` | News with sentiment scores and metadata |

The database uses WAL mode for concurrent access, allowing the WPF app to read while the collector writes.

---

## Project Structure

```
CryptoChartWPF/
├── src/
│   ├── CryptoChart.Core/        # Models, interfaces, enums (no dependencies)
│   ├── CryptoChart.Data/        # EF Core DbContext, repositories
│   ├── CryptoChart.Services/    # Binance, Finnhub, Alpha Vantage clients
│   └── CryptoChart.App/         # WPF application
├── tools/
│   └── CryptoChart.Collector/   # CLI data collection tool
├── tests/
│   └── CryptoChart.Tests/       # xUnit tests
├── docs/                        # Additional documentation
├── Architecture.md              # Technical architecture reference
└── README.md                    # This file
```

---

## Building from Source

```bash
# Build all projects
dotnet build CryptoChartWPF.sln

# Run tests
dotnet test

# Run WPF app
dotnet run --project src/CryptoChart.App

# Run collector
dotnet run --project tools/CryptoChart.Collector -- --help
```

---

## WPF Techniques Demonstrated

This project showcases advanced WPF development skills:

- **Custom Controls** - `DrawingVisual` for high-performance chart rendering
- **Dependency Properties** - Full data-binding support in custom controls
- **Routed Events** - Custom events for chart interactions (scroll, zoom, hover)
- **Resource Dictionaries** - Centralized theming with dark color palette
- **MVVM Architecture** - Clean separation with CommunityToolkit.Mvvm
- **Async/Await** - Proper UI thread marshalling with cancellation support
- **Reactive Extensions** - Rx.NET for throttling rapid UI events
- **UI Virtualization** - VirtualizingStackPanel for large news lists

---

## License

MIT License - See [LICENSE](LICENSE) for details.

---

## Acknowledgments

- [Binance API](https://binance-docs.github.io/apidocs/) - Market data
- [Finnhub](https://finnhub.io/) - News data
- [Alpha Vantage](https://www.alphavantage.co/) - News sentiment analysis
