using CryptoChart.Core.Interfaces;
using CryptoChart.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoChart.Services.News;

/// <summary>
/// Extension methods for registering news services with dependency injection.
/// </summary>
public static class NewsServiceExtensions
{
    /// <summary>
    /// Adds all news-related services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNewsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind settings
        var newsSection = configuration.GetSection(NewsServicesSettings.SectionName);
        
        services.Configure<FinnhubSettings>(
            newsSection.GetSection(nameof(NewsServicesSettings.Finnhub)));
        services.Configure<AlphaVantageSettings>(
            newsSection.GetSection(nameof(NewsServicesSettings.AlphaVantage)));

        // Register repository
        services.AddScoped<INewsRepository, NewsRepository>();

        // Register Finnhub service with HttpClient
        var finnhubSettings = newsSection.GetSection(nameof(NewsServicesSettings.Finnhub))
            .Get<FinnhubSettings>() ?? new FinnhubSettings();
        
        if (finnhubSettings.Enabled)
        {
            // Register typed HttpClient for FinnhubNewsService
            services.AddHttpClient<FinnhubNewsService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            
            // Register as INewsService using factory to get the typed HttpClient instance
            services.AddScoped<INewsService>(sp => sp.GetRequiredService<FinnhubNewsService>());
        }

        // Register Alpha Vantage service with HttpClient
        var alphaVantageSettings = newsSection.GetSection(nameof(NewsServicesSettings.AlphaVantage))
            .Get<AlphaVantageSettings>() ?? new AlphaVantageSettings();
        
        if (alphaVantageSettings.Enabled)
        {
            // Register typed HttpClient for AlphaVantageNewsService
            services.AddHttpClient<AlphaVantageNewsService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            
            // Register as INewsService using factory to get the typed HttpClient instance
            services.AddScoped<INewsService>(sp => sp.GetRequiredService<AlphaVantageNewsService>());
        }

        // Register aggregated service
        services.AddScoped<AggregatedNewsService>();

        return services;
    }

    /// <summary>
    /// Adds news services with explicit settings (for testing or manual configuration).
    /// </summary>
    public static IServiceCollection AddNewsServices(
        this IServiceCollection services,
        FinnhubSettings? finnhubSettings = null,
        AlphaVantageSettings? alphaVantageSettings = null)
    {
        // Register repository
        services.AddScoped<INewsRepository, NewsRepository>();

        // Configure Finnhub settings and service
        if (finnhubSettings != null)
        {
            services.Configure<FinnhubSettings>(options =>
            {
                options.ApiKey = finnhubSettings.ApiKey;
                options.BaseUrl = finnhubSettings.BaseUrl;
                options.RateLimitPerMinute = finnhubSettings.RateLimitPerMinute;
            });

            if (finnhubSettings.Enabled)
            {
                services.AddHttpClient<FinnhubNewsService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
                services.AddScoped<INewsService>(sp => sp.GetRequiredService<FinnhubNewsService>());
            }
        }

        // Configure Alpha Vantage settings and service
        if (alphaVantageSettings != null)
        {
            services.Configure<AlphaVantageSettings>(options =>
            {
                options.ApiKey = alphaVantageSettings.ApiKey;
                options.BaseUrl = alphaVantageSettings.BaseUrl;
                options.RateLimitPerDay = alphaVantageSettings.RateLimitPerDay;
            });

            if (alphaVantageSettings.Enabled)
            {
                services.AddHttpClient<AlphaVantageNewsService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
                services.AddScoped<INewsService>(sp => sp.GetRequiredService<AlphaVantageNewsService>());
            }
        }

        // Register aggregated service
        services.AddScoped<AggregatedNewsService>();

        return services;
    }
}
