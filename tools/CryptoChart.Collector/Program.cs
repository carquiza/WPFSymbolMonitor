using CryptoChart.Collector;
using CryptoChart.Core.Interfaces;
using CryptoChart.Data.Context;
using CryptoChart.Data.Repositories;
using CryptoChart.Services.Binance;
using Microsoft.EntityFrameworkCore;
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

    // Define command line options
    var symbolOption = new Option<string?>(
        name: "--symbol",
        description: "Symbol to collect (e.g., BTCUSDT). If not specified, collects all active symbols.");

    var timeframeOption = new Option<string>(
        name: "--timeframe",
        getDefaultValue: () => "1d",
        description: "Timeframe to collect: 1h (hourly) or 1d (daily)");

    var backfillOption = new Option<bool>(
        name: "--backfill",
        description: "Perform historical backfill (5 years for daily, 1 year for hourly)");

    var continuousOption = new Option<bool>(
        name: "--continuous",
        description: "Run in continuous mode, collecting new data periodically");

    var rootCommand = new RootCommand("CryptoChart Data Collector")
    {
        symbolOption,
        timeframeOption,
        backfillOption,
        continuousOption
    };

    rootCommand.SetHandler(async (symbol, timeframe, backfill, continuous) =>
    {
        var host = CreateHostBuilder().Build();
        var collector = host.Services.GetRequiredService<DataCollector>();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Log.Information("Shutdown requested...");
            cts.Cancel();
        };

        try
        {
            if (backfill)
            {
                await collector.BackfillAsync(symbol, timeframe, cts.Token);
            }

            if (continuous)
            {
                await collector.RunContinuousAsync(symbol, timeframe, cts.Token);
            }

            if (!backfill && !continuous)
            {
                // Default: just fetch latest data
                await collector.FetchLatestAsync(symbol, timeframe, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("Operation cancelled.");
        }
    }, symbolOption, timeframeOption, backfillOption, continuousOption);

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

static IHostBuilder CreateHostBuilder() =>
    Host.CreateDefaultBuilder()
        .UseSerilog()
        .ConfigureServices((_, services) =>
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

            // HTTP Client for Binance
            services.AddHttpClient<IMarketDataService, BinanceMarketDataService>();

            // Collector service
            services.AddScoped<DataCollector>();
        });
