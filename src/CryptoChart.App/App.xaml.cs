using System.IO;
using System.Windows;
using CryptoChart.App.ViewModels;
using CryptoChart.App.Views;
using CryptoChart.Core.Interfaces;
using CryptoChart.Data.Context;
using CryptoChart.Data.Repositories;
using CryptoChart.Services.Binance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace CryptoChart.App;

/// <summary>
/// Application entry point with dependency injection configuration.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
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

        // Realtime service (singleton for WebSocket connection)
        services.AddSingleton<IRealtimeMarketService, BinanceRealtimeService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ChartViewModel>();

        // Windows
        services.AddTransient<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure database is created and migrated
        using (var scope = _serviceProvider!.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CryptoDbContext>();
            await context.Database.EnsureCreatedAsync();
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Cleanup realtime service
        if (_serviceProvider != null)
        {
            var realtimeService = _serviceProvider.GetService<IRealtimeMarketService>();
            if (realtimeService != null)
            {
                await realtimeService.DisposeAsync();
            }
            
            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }
}
