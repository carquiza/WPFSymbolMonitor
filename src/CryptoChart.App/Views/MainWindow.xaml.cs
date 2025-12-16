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
    /// Clears hover state from both chart and news panel.
    /// </summary>
    private void OnChartMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Clear chart tooltip
        ViewModel.ChartViewModel.SetHoveredCandle(-1);

        // Clear news panel highlight
        _hoverStream.OnHover(-1);
    }
}
