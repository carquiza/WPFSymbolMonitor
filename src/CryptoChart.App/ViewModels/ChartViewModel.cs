using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CryptoChart.Core.Models;

namespace CryptoChart.App.ViewModels;

/// <summary>
/// ViewModel for the candlestick chart.
/// Manages candle data, visible range, and chart interactions.
/// </summary>
public partial class ChartViewModel : ObservableObject
{
    private readonly List<Candle> _allCandles = new();
    private readonly object _candleLock = new();

    /// <summary>
    /// Event raised when the visible range changes (scroll, zoom, or data update).
    /// </summary>
    public event EventHandler<VisibleRangeChangedEventArgs>? VisibleRangeChanged;

    public ChartViewModel()
    {
        VisibleCandles = new ObservableCollection<Candle>();
    }

    #region Properties

    [ObservableProperty]
    private ObservableCollection<Candle> _visibleCandles;

    [ObservableProperty]
    private int _visibleCandleCount = 100;

    [ObservableProperty]
    private int _scrollOffset;

    [ObservableProperty]
    private Candle? _hoveredCandle;

    [ObservableProperty]
    private bool _showCrosshair;

    [ObservableProperty]
    private double _crosshairX;

    [ObservableProperty]
    private double _crosshairY;

    [ObservableProperty]
    private decimal _minPrice;

    [ObservableProperty]
    private decimal _maxPrice;

    [ObservableProperty]
    private DateTime _startTime;

    [ObservableProperty]
    private DateTime _endTime;

    [ObservableProperty]
    private bool _hasData;

    public int TotalCandleCount => _allCandles.Count;

    public int MaxScrollOffset => Math.Max(0, TotalCandleCount - VisibleCandleCount);

    #endregion

    #region Data Management

    /// <summary>
    /// Updates the candle data with a new set of candles.
    /// </summary>
    public void UpdateCandles(IEnumerable<Candle> candles)
    {
        lock (_candleLock)
        {
            _allCandles.Clear();
            _allCandles.AddRange(candles.OrderBy(c => c.OpenTime));
        }

        // Reset scroll to show latest data
        ScrollOffset = MaxScrollOffset;
        UpdateVisibleRange();

        HasData = _allCandles.Count > 0;
    }

    /// <summary>
    /// Gets all candles for sentiment calculation.
    /// </summary>
    public IEnumerable<Candle> GetAllCandles()
    {
        lock (_candleLock)
        {
            return _allCandles.ToList();
        }
    }

    /// <summary>
    /// Updates the latest candle with real-time data.
    /// </summary>
    public void UpdateLatestCandle(Candle candle, bool isClosed)
    {
        lock (_candleLock)
        {
            if (_allCandles.Count == 0)
            {
                _allCandles.Add(candle);
            }
            else
            {
                var lastCandle = _allCandles[^1];

                if (lastCandle.OpenTime == candle.OpenTime)
                {
                    // Update existing candle
                    _allCandles[^1] = candle;
                }
                else if (isClosed || candle.OpenTime > lastCandle.OpenTime)
                {
                    // Add new candle
                    _allCandles.Add(candle);

                    // If we're scrolled to the end, keep scrolled to end
                    if (ScrollOffset >= MaxScrollOffset - 1)
                    {
                        ScrollOffset = MaxScrollOffset;
                    }
                }
            }
        }

        UpdateVisibleRange();
    }

    /// <summary>
    /// Gets the close price of the candle before the latest one.
    /// </summary>
    public decimal? GetPreviousClose()
    {
        lock (_candleLock)
        {
            return _allCandles.Count > 1 ? _allCandles[^2].Close : null;
        }
    }

    #endregion

    #region Visible Range Management

    private void UpdateVisibleRange()
    {
        lock (_candleLock)
        {
            if (_allCandles.Count == 0)
            {
                VisibleCandles.Clear();
                MinPrice = 0;
                MaxPrice = 0;
                return;
            }

            var startIndex = Math.Max(0, ScrollOffset);
            var endIndex = Math.Min(_allCandles.Count, startIndex + VisibleCandleCount);
            var visibleCandlesList = _allCandles.Skip(startIndex).Take(endIndex - startIndex).ToList();

            // Update collection
            VisibleCandles.Clear();
            foreach (var candle in visibleCandlesList)
            {
                VisibleCandles.Add(candle);
            }

            // Update price range with padding
            if (visibleCandlesList.Count > 0)
            {
                var minLow = visibleCandlesList.Min(c => c.Low);
                var maxHigh = visibleCandlesList.Max(c => c.High);
                var padding = (maxHigh - minLow) * 0.05m;

                MinPrice = minLow - padding;
                MaxPrice = maxHigh + padding;

                StartTime = visibleCandlesList.First().OpenTime;
                EndTime = visibleCandlesList.Last().CloseTime;
            }
        }

        // Notify property changes
        OnPropertyChanged(nameof(TotalCandleCount));
        OnPropertyChanged(nameof(MaxScrollOffset));

        // Raise visible range changed event for news/sentiment synchronization
        RaiseVisibleRangeChanged();
    }

    private void RaiseVisibleRangeChanged()
    {
        VisibleRangeChanged?.Invoke(this, new VisibleRangeChangedEventArgs(
            ScrollOffset,
            VisibleCandleCount,
            StartTime,
            EndTime
        ));
    }

    /// <summary>
    /// Scrolls the chart by a delta amount.
    /// </summary>
    public void Scroll(int delta)
    {
        var newOffset = ScrollOffset + delta;
        ScrollOffset = Math.Clamp(newOffset, 0, MaxScrollOffset);
        UpdateVisibleRange();
    }

    /// <summary>
    /// Zooms the chart by changing the visible candle count.
    /// </summary>
    public void Zoom(int delta)
    {
        var newCount = VisibleCandleCount + delta;
        VisibleCandleCount = Math.Clamp(newCount, 20, 500);

        // Adjust scroll to keep center point
        ScrollOffset = Math.Clamp(ScrollOffset, 0, MaxScrollOffset);
        UpdateVisibleRange();
    }

    /// <summary>
    /// Sets the hovered candle based on chart coordinates.
    /// </summary>
    public void SetHoveredCandle(int candleIndex)
    {
        if (candleIndex >= 0 && candleIndex < VisibleCandles.Count)
        {
            HoveredCandle = VisibleCandles[candleIndex];
            ShowCrosshair = true;
        }
        else
        {
            HoveredCandle = null;
            ShowCrosshair = false;
        }
    }

    /// <summary>
    /// Clears the hovered state.
    /// </summary>
    public void ClearHover()
    {
        HoveredCandle = null;
        ShowCrosshair = false;
    }

    #endregion
}

/// <summary>
/// Event args for visible range changes.
/// </summary>
public class VisibleRangeChangedEventArgs : EventArgs
{
    public int ScrollOffset { get; }
    public int VisibleCandleCount { get; }
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }

    public VisibleRangeChangedEventArgs(int scrollOffset, int visibleCandleCount, DateTime startTime, DateTime endTime)
    {
        ScrollOffset = scrollOffset;
        VisibleCandleCount = visibleCandleCount;
        StartTime = startTime;
        EndTime = endTime;
    }
}
