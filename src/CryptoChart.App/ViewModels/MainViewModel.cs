using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoChart.App.Infrastructure;
using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;
using CryptoChart.Services.News;

namespace CryptoChart.App.ViewModels;

/// <summary>
/// Main ViewModel for the application.
/// Manages symbol selection, timeframe, and coordinates data loading.
/// 
/// Threading model:
/// - All public methods are safe to call from UI thread
/// - Uses CancellationManager to cancel stale requests when selection changes rapidly
/// - Uses AsyncDebouncer for hover events to prevent excessive lookups
/// - Real-time updates use BeginInvoke for non-blocking UI updates
/// </summary>
public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ISymbolRepository _symbolRepository;
    private readonly ICandleRepository _candleRepository;
    private readonly INewsRepository _newsRepository;
    private readonly IMarketDataService _marketDataService;
    private readonly IRealtimeMarketService _realtimeService;

    // Cancellation manager for data loading - cancels previous load when new selection made
    private readonly CancellationManager _loadCancellation = new();
    
    // Debouncer for hover events - prevents excessive lookups during rapid mouse movement
    // Currently hover uses in-memory data, but this enables future DB-backed lookups
    private readonly AsyncDebouncer _hoverDebouncer;

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

        // Initialize hover debouncer with 50ms delay - responsive but prevents spam
        // Uses the dispatcher to ensure UI updates happen on main thread
        _hoverDebouncer = new AsyncDebouncer(
            delayMilliseconds: 50,
            dispatcher: System.Windows.Application.Current.Dispatcher);
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
            var ct = _loadCancellation.GetToken();
            var symbols = await _symbolRepository.GetActiveAsync(ct);
            
            // Check if cancelled before updating UI
            if (ct.IsCancellationRequested) return;
            
            Symbols = new ObservableCollection<Symbol>(symbols);

            if (Symbols.Count > 0 && SelectedSymbol == null)
            {
                SelectedSymbol = Symbols.First();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when user changes selection - ignore
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

        // Get a fresh cancellation token - this cancels any previous load operation
        var ct = _loadCancellation.GetToken();

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Unsubscribe from previous symbol
            await _realtimeService.UnsubscribeAllAsync();

            // Check cancellation before expensive DB call
            ct.ThrowIfCancellationRequested();

            // Try to load from database first
            var dbCandles = await _candleRepository.GetLatestCandlesAsync(
                SelectedSymbol.Id,
                SelectedTimeFrame,
                500,
                ct);

            var candleList = dbCandles.ToList();

            // Check cancellation after DB call
            ct.ThrowIfCancellationRequested();

            // If no data in DB, fetch from API
            if (candleList.Count == 0)
            {
                var apiCandles = await _marketDataService.GetLatestCandlesAsync(
                    SelectedSymbol.Name,
                    SelectedTimeFrame,
                    500);

                candleList = apiCandles.ToList();

                ct.ThrowIfCancellationRequested();

                // Store in database for future use
                foreach (var candle in candleList)
                {
                    candle.SymbolId = SelectedSymbol.Id;
                }
                await _candleRepository.AddRangeAsync(candleList, ct);
            }

            ct.ThrowIfCancellationRequested();

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
            await LoadNewsAsync(candleList, ct);

            ct.ThrowIfCancellationRequested();

            // Subscribe to real-time updates
            await _realtimeService.SubscribeAsync(SelectedSymbol.Name, SelectedTimeFrame);
        }
        catch (OperationCanceledException)
        {
            // Expected when user changes selection rapidly - ignore
            System.Diagnostics.Debug.WriteLine("Load cancelled - user changed selection");
        }
        catch (Exception ex)
        {
            // Only show error if this wasn't cancelled
            if (!ct.IsCancellationRequested)
            {
                ErrorMessage = $"Failed to load data: {ex.Message}";
            }
        }
        finally
        {
            // Only clear loading if this is still the active operation
            if (!ct.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    private async Task LoadNewsAsync(List<Candle> candles, CancellationToken ct)
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

            ct.ThrowIfCancellationRequested();

            // Load news for this symbol and time range
            var articles = await _newsRepository.GetNewsAsync(
                baseAsset, 
                startTime, 
                endTime,
                ct);

            ct.ThrowIfCancellationRequested();

            var articleList = articles.ToList();
            System.Diagnostics.Debug.WriteLine($"Found {articleList.Count} news articles for {baseAsset}");

            // Update the news view model with articles
            NewsViewModel.UpdateArticles(articleList);
            
            // Calculate sentiments for ALL candles (not just visible ones)
            NewsViewModel.CalculateSentiments(candles);
            
            // The visible range will be updated via the VisibleRangeChanged event
        }
        catch (OperationCanceledException)
        {
            // Expected - rethrow so caller handles it
            throw;
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
    /// Uses debouncing to prevent excessive updates during rapid mouse movement.
    /// Currently updates in-memory data; infrastructure supports future DB lookups.
    /// </summary>
    public void OnCandleHovered(int candleIndex)
    {
        // Use debouncer for future DB-backed lookups
        // For now, the actual update is fast (in-memory), but this demonstrates
        // the pattern for when you add features like fetching detailed article content
        _hoverDebouncer.Trigger(candleIndex, (index, ct) =>
        {
            if (ct.IsCancellationRequested) return Task.CompletedTask;

            ChartViewModel.SetHoveredCandle(index);

            // Pass the actual candle to NewsViewModel for article filtering
            var hoveredCandle = index >= 0 && index < ChartViewModel.VisibleCandles.Count
                ? ChartViewModel.VisibleCandles[index]
                : null;

            NewsViewModel.SetHighlightedIndex(index, hoveredCandle);

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Called by MainWindow when mouse leaves the chart.
    /// Clears hover state from both view models.
    /// </summary>
    public void OnCandleHoverCleared()
    {
        // Cancel any pending hover operation
        _hoverDebouncer.Cancel();
        
        ChartViewModel.ClearHover();
        NewsViewModel.ClearHighlight();
    }

    #endregion

    #region Real-time Updates

    private void OnCandleUpdated(object? sender, CandleUpdateEventArgs e)
    {
        if (SelectedSymbol == null || e.Symbol != SelectedSymbol.Name)
            return;

        // Use BeginInvoke for non-blocking UI updates
        // This is critical for responsive UI - Invoke blocks the calling thread
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
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
        // Use BeginInvoke for non-blocking UI updates
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
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
        // Cancel any pending operations
        _loadCancellation.Cancel();
        _hoverDebouncer.Cancel();

        // Unsubscribe from events
        _realtimeService.CandleUpdated -= OnCandleUpdated;
        _realtimeService.ConnectionStatusChanged -= OnConnectionStatusChanged;
        ChartViewModel.VisibleRangeChanged -= OnVisibleRangeChanged;

        // Clean up realtime service
        await _realtimeService.UnsubscribeAllAsync();

        // Dispose infrastructure
        _loadCancellation.Dispose();
        _hoverDebouncer.Dispose();
    }

    #endregion
}
