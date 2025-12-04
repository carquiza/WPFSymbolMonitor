using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using CryptoChart.Services.News;

namespace CryptoChart.App.ViewModels;

/// <summary>
/// Main ViewModel for the application.
/// Manages symbol selection, timeframe, and coordinates data loading.
/// </summary>
public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ISymbolRepository _symbolRepository;
    private readonly ICandleRepository _candleRepository;
    private readonly INewsRepository _newsRepository;
    private readonly IMarketDataService _marketDataService;
    private readonly IRealtimeMarketService _realtimeService;

    public MainViewModel(
        ISymbolRepository symbolRepository,
        ICandleRepository candleRepository,
        IMarketDataService marketDataService,
        IRealtimeMarketService realtimeService,
        INewsRepository newsRepository)
    {
        _symbolRepository = symbolRepository;
        _candleRepository = candleRepository;
        _marketDataService = marketDataService;
        _realtimeService = realtimeService;
        _newsRepository = newsRepository;

        _realtimeService.CandleUpdated += OnCandleUpdated;
        _realtimeService.ConnectionStatusChanged += OnConnectionStatusChanged;

        // Create the child view models
        ChartViewModel = new ChartViewModel();
        NewsViewModel = new NewsViewModel();

        // Subscribe to visible range changes for sentiment sync
        ChartViewModel.VisibleRangeChanged += OnVisibleRangeChanged;
    }

    #region Properties

    [ObservableProperty]
    private ChartViewModel _chartViewModel;

    [ObservableProperty]
    private NewsViewModel _newsViewModel;

    [ObservableProperty]
    private ObservableCollection<Symbol> _symbols = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPriceDisplay))]
    [NotifyPropertyChangedFor(nameof(PriceChangeDisplay))]
    [NotifyPropertyChangedFor(nameof(IsPriceUp))]
    private Symbol? _selectedSymbol;

    [ObservableProperty]
    private TimeFrame _selectedTimeFrame = TimeFrame.Daily;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPriceDisplay))]
    [NotifyPropertyChangedFor(nameof(PriceChangeDisplay))]
    [NotifyPropertyChangedFor(nameof(IsPriceUp))]
    private decimal _currentPrice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PriceChangeDisplay))]
    [NotifyPropertyChangedFor(nameof(IsPriceUp))]
    private decimal _priceChange;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PriceChangeDisplay))]
    [NotifyPropertyChangedFor(nameof(IsPriceUp))]
    private decimal _priceChangePercent;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public string CurrentPriceDisplay => SelectedSymbol != null
        ? FormatPrice(CurrentPrice, SelectedSymbol.QuoteAsset)
        : "--";

    public string PriceChangeDisplay => PriceChangePercent != 0
        ? $"{(PriceChange >= 0 ? "+" : "")}{PriceChange:N2} ({PriceChangePercent:N2}%)"
        : "--";

    public bool IsPriceUp => PriceChange >= 0;

    #endregion

    #region Commands

    [RelayCommand]
    private async Task LoadSymbolsAsync()
    {
        try
        {
            var symbols = await _symbolRepository.GetActiveAsync();
            Symbols = new ObservableCollection<Symbol>(symbols);

            if (Symbols.Count > 0 && SelectedSymbol == null)
            {
                SelectedSymbol = Symbols.First();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load symbols: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectTimeFrameAsync(TimeFrame timeFrame)
    {
        if (SelectedTimeFrame == timeFrame)
            return;

        SelectedTimeFrame = timeFrame;
        await LoadCandlesAsync();
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (SelectedSymbol == null)
            return;

        await LoadCandlesAsync();
    }

    #endregion

    #region Data Loading

    async partial void OnSelectedSymbolChanged(Symbol? value)
    {
        if (value != null)
        {
            await LoadCandlesAsync();
        }
    }

    private async Task LoadCandlesAsync()
    {
        if (SelectedSymbol == null)
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Unsubscribe from previous symbol
            await _realtimeService.UnsubscribeAllAsync();

            // Try to load from database first
            var dbCandles = await _candleRepository.GetLatestCandlesAsync(
                SelectedSymbol.Id,
                SelectedTimeFrame,
                500);

            var candleList = dbCandles.ToList();

            // If no data in DB, fetch from API
            if (candleList.Count == 0)
            {
                var apiCandles = await _marketDataService.GetLatestCandlesAsync(
                    SelectedSymbol.Name,
                    SelectedTimeFrame,
                    500);

                candleList = apiCandles.ToList();

                // Store in database for future use
                foreach (var candle in candleList)
                {
                    candle.SymbolId = SelectedSymbol.Id;
                }
                await _candleRepository.AddRangeAsync(candleList);
            }

            // Update chart
            ChartViewModel.UpdateCandles(candleList);

            // Update current price info
            if (candleList.Count > 0)
            {
                var latest = candleList.Last();
                var previousClose = candleList.Count > 1 ? candleList[^2].Close : latest.Open;

                CurrentPrice = latest.Close;
                PriceChange = latest.Close - previousClose;
                PriceChangePercent = previousClose != 0
                    ? (PriceChange / previousClose) * 100
                    : 0;
            }

            // Load news data if repository is available
            await LoadNewsAsync(candleList);

            // Subscribe to real-time updates
            await _realtimeService.SubscribeAsync(SelectedSymbol.Name, SelectedTimeFrame);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadNewsAsync(List<Candle> candles)
    {
        if (SelectedSymbol == null || candles.Count == 0)
            return;

        try
        {
            // Get the time range from ALL candles (for loading news)
            var startTime = candles.First().OpenTime;
            var endTime = candles.Last().CloseTime;

            // Convert trading pair (BTCUSDT) to base asset (BTC) for news query
            var baseAsset = CryptoSymbolMapper.GetBaseAsset(SelectedSymbol.Name);
            
            System.Diagnostics.Debug.WriteLine($"Loading news for {baseAsset} from {startTime} to {endTime}");

            // Load news for this symbol and time range
            var articles = await _newsRepository.GetNewsAsync(
                baseAsset, 
                startTime, 
                endTime);

            var articleList = articles.ToList();
            System.Diagnostics.Debug.WriteLine($"Found {articleList.Count} news articles for {baseAsset}");

            // Update the news view model with articles
            NewsViewModel.UpdateArticles(articleList);
            
            // Calculate sentiments for ALL candles (not just visible ones)
            NewsViewModel.CalculateSentiments(candles);
            
            // The visible range will be updated via the VisibleRangeChanged event
        }
        catch (Exception ex)
        {
            // News loading is non-critical, just log the error
            System.Diagnostics.Debug.WriteLine($"Failed to load news: {ex.Message}");
        }
    }

    #endregion

    #region Visible Range Synchronization

    /// <summary>
    /// Called when the chart's visible range changes (scroll, zoom, or initial load).
    /// Synchronizes the news/sentiment view models.
    /// </summary>
    private void OnVisibleRangeChanged(object? sender, VisibleRangeChangedEventArgs e)
    {
        NewsViewModel.UpdateVisibleWindow(
            e.ScrollOffset,
            e.VisibleCandleCount,
            e.StartTime,
            e.EndTime
        );
    }

    /// <summary>
    /// Called by MainWindow when a candle is hovered.
    /// Updates both chart and news view models.
    /// </summary>
    public void OnCandleHovered(int candleIndex)
    {
        ChartViewModel.SetHoveredCandle(candleIndex);

        // Pass the actual candle to NewsViewModel for article filtering
        var hoveredCandle = candleIndex >= 0 && candleIndex < ChartViewModel.VisibleCandles.Count
            ? ChartViewModel.VisibleCandles[candleIndex]
            : null;

        NewsViewModel.SetHighlightedIndex(candleIndex, hoveredCandle);
    }

    /// <summary>
    /// Called by MainWindow when mouse leaves the chart.
    /// Clears hover state from both view models.
    /// </summary>
    public void OnCandleHoverCleared()
    {
        ChartViewModel.ClearHover();
        NewsViewModel.ClearHighlight();
    }

    #endregion

    #region Real-time Updates

    private void OnCandleUpdated(object? sender, CandleUpdateEventArgs e)
    {
        if (SelectedSymbol == null || e.Symbol != SelectedSymbol.Name)
            return;

        // Update on UI thread
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ChartViewModel.UpdateLatestCandle(e.Candle, e.IsClosed);

            // Update price display
            CurrentPrice = e.Candle.Close;
            var previousClose = ChartViewModel.GetPreviousClose() ?? e.Candle.Open;
            PriceChange = e.Candle.Close - previousClose;
            PriceChangePercent = previousClose != 0
                ? (PriceChange / previousClose) * 100
                : 0;
        });
    }

    private void OnConnectionStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsConnected = e.IsConnected;
            ConnectionStatus = e.Message ?? (e.IsConnected ? "Connected" : "Disconnected");

            if (e.Error != null)
            {
                ErrorMessage = $"Connection error: {e.Error.Message}";
            }
        });
    }

    #endregion

    #region Helpers

    private static string FormatPrice(decimal price, string quoteAsset)
    {
        // Format based on price magnitude and quote asset
        return quoteAsset.ToUpper() switch
        {
            "USDT" or "USD" => price switch
            {
                >= 1000 => $"${price:N2}",
                >= 1 => $"${price:N4}",
                _ => $"${price:N6}"
            },
            "BTC" => price switch
            {
                >= 1 => $"{price:N4} BTC",
                _ => $"{price:N8} BTC"
            },
            _ => $"{price:N8} {quoteAsset}"
        };
    }

    #endregion

    #region Disposal

    public async ValueTask DisposeAsync()
    {
        _realtimeService.CandleUpdated -= OnCandleUpdated;
        _realtimeService.ConnectionStatusChanged -= OnConnectionStatusChanged;
        ChartViewModel.VisibleRangeChanged -= OnVisibleRangeChanged;
        await _realtimeService.UnsubscribeAllAsync();
    }

    #endregion
}
