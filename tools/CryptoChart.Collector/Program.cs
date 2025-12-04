using CryptoChart.Collector;
using CryptoChart.Core.Interfaces;
using CryptoChart.Data.Context;
using CryptoChart.Data.Repositories;
using CryptoChart.Services.Binance;
using CryptoChart.Services.News;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.CommandLine;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/collector-.log", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("CryptoChart Collector starting...");

    // Define command line options - Market Data
    var symbolOption = new Option<string?>(
        name: "--symbol",
        description: "Symbol to collect (e.g., BTCUSDT). If not specified, collects all active symbols.");

    var timeframeOption = new Option<string>(
        name: "--timeframe",
        getDefaultValue: () => "1d",
        description: "Timeframe to collect: 1h (hourly) or 1d (daily)");

    var backfillOption = new Option<bool>(
        name: "--backfill",
        description: "Perform historical candle backfill (5 years for daily, 1 year for hourly)");

    var continuousOption = new Option<bool>(
        name: "--continuous",
        description: "Run in continuous mode, collecting new data periodically");

    // Define command line options - News
    var collectNewsOption = new Option<bool>(
        name: "--collect-news",
        description: "Fetch latest news articles for crypto symbols");

    var newsBackfillOption = new Option<bool>(
        name: "--news-backfill",
        description: "Perform historical news backfill");

    var newsDaysOption = new Option<int?>(
        name: "--news-days",
        description: "Number of days to backfill news (default: 30)");

    var newsStatsOption = new Option<bool>(
        name: "--news-stats",
        description: "Show news database statistics");

    var generalNewsOption = new Option<bool>(
        name: "--general-news",
        description: "Fetch general crypto news (not symbol-specific)");

    var rootCommand = new RootCommand("CryptoChart Data Collector - Market data and news collection")
    {
        symbolOption,
        timeframeOption,
        backfillOption,
        continuousOption,
        collectNewsOption,
        newsBackfillOption,
        newsDaysOption,
        newsStatsOption,
        generalNewsOption
    };

    rootCommand.SetHandler(async (context) =>
    {
        var symbol = context.ParseResult.GetValueForOption(symbolOption);
        var timeframe = context.ParseResult.GetValueForOption(timeframeOption)!;
        var backfill = context.ParseResult.GetValueForOption(backfillOption);
        var continuous = context.ParseResult.GetValueForOption(continuousOption);
        var collectNews = context.ParseResult.GetValueForOption(collectNewsOption);
        var newsBackfill = context.ParseResult.GetValueForOption(newsBackfillOption);
        var newsDays = context.ParseResult.GetValueForOption(newsDaysOption);
        var newsStats = context.ParseResult.GetValueForOption(newsStatsOption);
        var generalNews = context.ParseResult.GetValueForOption(generalNewsOption);

        var host = CreateHostBuilder().Build();

        // Ensure database is created and schema is up to date
        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CryptoDbContext>();
            await EnsureDatabaseSchemaAsync(dbContext);
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Log.Information("Shutdown requested...");
            cts.Cancel();
        };

        try
        {
            // Check if any news operations were requested
            var newsOperationRequested = collectNews || newsBackfill || newsStats || generalNews;

            // News operations
            if (newsOperationRequested)
            {
                using var scope = host.Services.CreateScope();
                var newsCollector = scope.ServiceProvider.GetService<NewsCollector>();

                if (newsCollector == null)
                {
                    Log.Warning("News services not configured. Please add API keys to appsettings.json");
                    Log.Information("See docs/NEWS_SERVICE_TODO.md for configuration instructions.");
                }
                else
                {
                    if (newsStats)
                    {
                        await newsCollector.ShowNewsStatsAsync(cts.Token);
                    }

                    if (generalNews)
                    {
                        await newsCollector.FetchGeneralNewsAsync(50, cts.Token);
                    }

                    if (newsBackfill)
                    {
                        await newsCollector.BackfillNewsAsync(symbol, newsDays, cts.Token);
                    }

                    if (collectNews)
                    {
                        if (continuous)
                        {
                            await newsCollector.RunContinuousNewsAsync(symbol, cts.Token);
                        }
                        else
                        {
                            await newsCollector.FetchLatestNewsAsync(symbol, cts.Token);
                        }
                    }
                }
            }

            // Market data operations (only if no news-only operation, or if explicitly requested)
            var marketDataRequested = backfill || (!newsOperationRequested);

            if (marketDataRequested || (continuous && !collectNews))
            {
                using var scope = host.Services.CreateScope();
                var collector = scope.ServiceProvider.GetRequiredService<DataCollector>();

                if (backfill)
                {
                    await collector.BackfillAsync(symbol, timeframe, cts.Token);
                }

                if (continuous && !collectNews)
                {
                    await collector.RunContinuousAsync(symbol, timeframe, cts.Token);
                }

                if (!backfill && !continuous && !newsOperationRequested)
                {
                    // Default: just fetch latest market data
                    await collector.FetchLatestAsync(symbol, timeframe, cts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("Operation cancelled.");
        }
    });

    return await rootCommand.InvokeAsync(args);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.Information("CryptoChart Collector shutting down.");
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Ensures the database schema is up to date.
/// Creates the database if it doesn't exist, or adds missing tables to an existing database.
/// </summary>
static async Task EnsureDatabaseSchemaAsync(CryptoDbContext dbContext)
{
    var dbPath = dbContext.Database.GetDbConnection().DataSource;
    var dbExists = File.Exists(dbPath);

    if (!dbExists)
    {
        // Database doesn't exist - create it with full schema
        Log.Information("Creating new database at {Path}", dbPath);
        await dbContext.Database.EnsureCreatedAsync();
        return;
    }

    // Database exists - check if we need to add new tables
    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        // Check if NewsArticles table exists
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='NewsArticles';";
        var result = await command.ExecuteScalarAsync();

        if (result == null)
        {
            Log.Information("Adding NewsArticles table to existing database...");
            
            // Create the NewsArticles table manually
            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""NewsArticles"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_NewsArticles"" PRIMARY KEY AUTOINCREMENT,
                    ""ExternalId"" TEXT NOT NULL,
                    ""Source"" TEXT NOT NULL,
                    ""Symbol"" TEXT NOT NULL,
                    ""Headline"" TEXT NOT NULL,
                    ""Summary"" TEXT NULL,
                    ""Url"" TEXT NOT NULL,
                    ""ImageUrl"" TEXT NULL,
                    ""Publisher"" TEXT NULL,
                    ""PublishedAt"" TEXT NOT NULL,
                    ""SentimentScore"" TEXT NULL,
                    ""SentimentLabel"" TEXT NULL,
                    ""RelevanceScore"" TEXT NULL,
                    ""Category"" TEXT NULL,
                    ""RetrievedAt"" TEXT NOT NULL
                );
                
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_NewsArticles_ExternalId_Source"" 
                    ON ""NewsArticles"" (""ExternalId"", ""Source"");
                
                CREATE INDEX IF NOT EXISTS ""IX_NewsArticles_Symbol_PublishedAt"" 
                    ON ""NewsArticles"" (""Symbol"", ""PublishedAt"");
                
                CREATE INDEX IF NOT EXISTS ""IX_NewsArticles_PublishedAt"" 
                    ON ""NewsArticles"" (""PublishedAt"");
            ";
            await createCommand.ExecuteNonQueryAsync();
            
            Log.Information("NewsArticles table created successfully.");
        }
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static IHostBuilder CreateHostBuilder() =>
    Host.CreateDefaultBuilder()
        .UseSerilog()
        .ConfigureAppConfiguration((context, config) =>
        {
            config.SetBasePath(AppContext.BaseDirectory);
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", 
                optional: true, reloadOnChange: true);
            config.AddUserSecrets<Program>(optional: true);
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            // Database
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CryptoChart",
                "cryptodata.db");

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            services.AddDbContext<CryptoDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // Repositories
            services.AddScoped<ISymbolRepository, SymbolRepository>();
            services.AddScoped<ICandleRepository, CandleRepository>();
            services.AddScoped<INewsRepository, NewsRepository>();

            // HTTP Client for Binance
            services.AddHttpClient<IMarketDataService, BinanceMarketDataService>();

            // Market data collector service
            services.AddScoped<DataCollector>();

            // News services (optional - depends on configuration)
            var newsConfig = context.Configuration.GetSection(NewsServicesSettings.SectionName);
            var finnhubKey = newsConfig.GetSection("Finnhub:ApiKey").Value;
            var alphaVantageKey = newsConfig.GetSection("AlphaVantage:ApiKey").Value;

            var hasAnyNewsConfig = !string.IsNullOrWhiteSpace(finnhubKey) || 
                                   !string.IsNullOrWhiteSpace(alphaVantageKey);

            if (hasAnyNewsConfig)
            {
                services.AddNewsServices(context.Configuration);
                services.AddScoped<NewsCollector>();
                Log.Information("News services enabled. Finnhub: {Finnhub}, AlphaVantage: {AV}",
                    !string.IsNullOrWhiteSpace(finnhubKey) ? "Yes" : "No",
                    !string.IsNullOrWhiteSpace(alphaVantageKey) ? "Yes" : "No");
            }
            else
            {
                Log.Debug("News services not configured (no API keys found)");
            }
        });

// Marker class for user secrets
public partial class Program { }
