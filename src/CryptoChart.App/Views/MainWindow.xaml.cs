using System.Windows;
using CryptoChart.App.Controls;
using CryptoChart.App.Infrastructure;
using CryptoChart.App.ViewModels;

namespace CryptoChart.App.Views;

/// <summary>
/// Main window for the crypto chart application.
/// Uses reactive streams for throttled news panel updates.
/// </summary>
public partial class MainWindow : Window
{
    private readonly CandleHoverStream _hoverStream;
    private readonly IDisposable _hoverSubscription;

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow(MainViewModel viewModel, CandleHoverStream hoverStream)
    {
        InitializeComponent();
        DataContext = viewModel;
        _hoverStream = hoverStream;

        // Subscribe to throttled hover updates for news panel only
        // This fires at most every 50ms instead of every mouse move (60+ times/sec)
        _hoverSubscription = _hoverStream.Subscribe(
            OnThrottledHoverForNews,
            ex => System.Diagnostics.Debug.WriteLine($"Hover stream error: {ex.Message}"),
            () => { });

        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadSymbolsCommand.ExecuteAsync(null);
    }

    private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Dispose hover stream subscription
        _hoverSubscription.Dispose();
        
        await ViewModel.DisposeAsync();
    }

    private void OnChartScrollRequested(object sender, ScrollEventArgs e)
    {
        ViewModel.ChartViewModel.Scroll(e.Delta);
    }

    private void OnChartZoomRequested(object sender, ZoomEventArgs e)
    {
        ViewModel.ChartViewModel.Zoom(e.Delta);
    }

    /// <summary>
    /// Raw hover event from chart control.
    /// Chart tooltip updates immediately for responsiveness.
    /// News panel updates are pushed to reactive stream for throttling.
    /// </summary>
    private void OnCandleHovered(object sender, CandleHoveredEventArgs e)
    {
        // Immediate: Update chart tooltip (needs to feel responsive)
        ViewModel.ChartViewModel.SetHoveredCandle(e.CandleIndex);

        // Throttled: Push to stream for news panel update
        // The stream will throttle and deduplicate before calling OnThrottledHoverForNews
        _hoverStream.OnHover(e.CandleIndex);
    }

    /// <summary>
    /// Handle click on a candle to select it for persistent news display.
    /// </summary>
    private void OnCandleClicked(object sender, CandleClickedEventArgs e)
    {
        // Toggle selection: clicking the same candle deselects it
        if (ViewModel.ChartViewModel.SelectedCandleIndex == e.CandleIndex)
        {
            ViewModel.ChartViewModel.ClearSelection();
            ViewModel.NewsViewModel?.ClearSelection();
        }
        else
        {
            ViewModel.ChartViewModel.SelectCandle(e.CandleIndex);
            var selectedCandle = ViewModel.ChartViewModel.SelectedCandle;
            if (selectedCandle != null)
            {
                ViewModel.NewsViewModel?.SetSelectedCandle(selectedCandle);
            }
        }
    }

    /// <summary>
    /// Throttled hover callback for news panel updates.
    /// Called at most every 50ms instead of every mouse move.
    /// This prevents UI stalls from cascading news panel updates.
    /// </summary>
    private void OnThrottledHoverForNews(int candleIndex)
    {
        // Only update news panel here - this was the expensive operation
        // Pass the actual candle so news can filter to that candle's time period
        var candle = candleIndex >= 0 ? ViewModel.ChartViewModel.HoveredCandle : null;
        ViewModel.NewsViewModel?.SetHighlightedIndex(candleIndex, candle);
    }

    /// <summary>
    /// Handle mouse leaving the chart area.
    /// Clears hover state but preserves selected candle for news.
    /// </summary>
    private void OnChartMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Clear chart tooltip
        ViewModel.ChartViewModel.SetHoveredCandle(-1);

        // Push -1 to hover stream - this will:
        // 1. Cancel any pending throttled hover events
        // 2. Eventually call SetHighlightedIndex(-1, null) which clears sentiment chart
        // 3. SetHighlightedIndex will skip clearing news panel if there's a pinned candle
        _hoverStream.OnHover(-1);
    }
}
