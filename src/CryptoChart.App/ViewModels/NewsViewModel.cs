using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CryptoChart.App.Infrastructure;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;

namespace CryptoChart.App.ViewModels;

/// <summary>
/// ViewModel for managing news data and sentiment aggregation.
/// Provides synchronized news and sentiment data for the chart.
/// </summary>
public partial class NewsViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly List<NewsArticle> _allArticles = new();
    private readonly List<CandleSentiment> _allSentiments = new();
    private readonly object _articlesLock = new();
    private readonly CancellationManager _articleFilterCancellation = new();
    private Candle? _pinnedCandle;  // The candle that was clicked/selected for persistent display

    public NewsViewModel()
    {
        VisibleArticles = new ObservableCollection<NewsArticle>();
        Sentiments = new ObservableCollection<CandleSentiment>();
        SelectedCandleArticles = new ObservableCollection<NewsArticle>();
    }

    #region Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedArticles))]
    private ObservableCollection<NewsArticle> _visibleArticles;

    [ObservableProperty]
    private ObservableCollection<CandleSentiment> _sentiments;

    /// <summary>
    /// Articles filtered by the currently selected/hovered candle's time range.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedArticles))]
    private ObservableCollection<NewsArticle> _selectedCandleArticles;

    /// <summary>
    /// When true, shows only articles for selected candle instead of all visible articles.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedArticles))]
    private bool _isShowingCandleSelection;

    /// <summary>
    /// The articles to display in the news panel - either filtered by candle or all visible.
    /// </summary>
    public ObservableCollection<NewsArticle> DisplayedArticles =>
        IsShowingCandleSelection && SelectedCandleArticles.Count > 0
            ? SelectedCandleArticles
            : VisibleArticles;

    [ObservableProperty]
    private NewsArticle? _hoveredArticle;

    [ObservableProperty]
    private CandleSentiment? _hoveredSentiment;

    [ObservableProperty]
    private int _highlightedIndex = -1;

    [ObservableProperty]
    private bool _isPanelExpanded = true;

    [ObservableProperty]
    private DateTime _visibleStartTime;

    [ObservableProperty]
    private DateTime _visibleEndTime;

    [ObservableProperty]
    private int _totalBullishCount;

    [ObservableProperty]
    private int _totalBearishCount;

    [ObservableProperty]
    private int _totalNeutralCount;

    /// <summary>
    /// Current scroll offset (matching ChartViewModel.ScrollOffset).
    /// </summary>
    private int _scrollOffset;

    /// <summary>
    /// Number of visible candles (matching ChartViewModel.VisibleCandleCount).
    /// </summary>
    private int _visibleCandleCount = 100;

    public int TotalArticleCount => _allArticles.Count;

    public bool HasArticles => _allArticles.Count > 0;

    /// <summary>
    /// Text shown in news panel header when filtering by candle.
    /// </summary>
    [ObservableProperty]
    private string _selectionHeaderText = "Market News";

    /// <summary>
    /// True when a candle has been clicked/pinned for persistent news display.
    /// </summary>
    [ObservableProperty]
    private bool _hasPinnedCandle;

    #endregion

    #region Data Management

    /// <summary>
    /// Updates the news data with articles from the repository.
    /// </summary>
    public void UpdateArticles(IEnumerable<NewsArticle> articles)
    {
        lock (_articlesLock)
        {
            _allArticles.Clear();
            _allArticles.AddRange(articles.OrderByDescending(a => a.PublishedAt));
        }

        UpdateCounts();
        OnPropertyChanged(nameof(TotalArticleCount));
        OnPropertyChanged(nameof(HasArticles));
    }

    /// <summary>
    /// Calculates sentiment aggregation for ALL candles.
    /// Call this once after loading candle data.
    /// </summary>
    public void CalculateSentiments(IEnumerable<Candle> allCandles)
    {
        var candleList = allCandles.OrderBy(c => c.OpenTime).ToList();

        lock (_articlesLock)
        {
            _allSentiments.Clear();

            foreach (var candle in candleList)
            {
                var matchingArticles = _allArticles
                    .Where(a => a.PublishedAt >= candle.OpenTime && a.PublishedAt < candle.CloseTime)
                    .ToList();

                var sentiment = new CandleSentiment
                {
                    OpenTime = candle.OpenTime,
                    CloseTime = candle.CloseTime,
                    BullishCount = matchingArticles.Count(a => a.IsBullish),
                    BearishCount = matchingArticles.Count(a => a.IsBearish),
                    NeutralCount = matchingArticles.Count(a => a.IsNeutral),
                    ArticleIds = matchingArticles.Select(a => a.Id).ToList()
                };

                _allSentiments.Add(sentiment);
            }
        }

        // Update the visible sentiments based on current scroll position
        UpdateVisibleSentiments();
    }

    /// <summary>
    /// Updates visible articles and sentiments based on the current scroll offset and visible count.
    /// Call this when the chart scrolls or zooms.
    /// </summary>
    public void UpdateVisibleWindow(int scrollOffset, int visibleCandleCount, DateTime startTime, DateTime endTime)
    {
        _scrollOffset = scrollOffset;
        _visibleCandleCount = visibleCandleCount;
        VisibleStartTime = startTime;
        VisibleEndTime = endTime;

        UpdateVisibleSentiments();
        UpdateVisibleArticles();
        UpdateCounts();
    }

    /// <summary>
    /// Updates visible articles based on the current time range.
    /// </summary>
    public void UpdateVisibleRange(DateTime startTime, DateTime endTime)
    {
        VisibleStartTime = startTime;
        VisibleEndTime = endTime;

        UpdateVisibleArticles();
        UpdateCounts();
    }

    private void UpdateVisibleArticles()
    {
        lock (_articlesLock)
        {
            var visible = _allArticles
                .Where(a => a.PublishedAt >= VisibleStartTime && a.PublishedAt <= VisibleEndTime)
                .OrderByDescending(a => a.PublishedAt)
                .ToList();

            // Batch update: single property change instead of Clear + N Adds
            VisibleArticles = new ObservableCollection<NewsArticle>(visible);
        }
        
        // Notify that DisplayedArticles may have changed
        OnPropertyChanged(nameof(DisplayedArticles));
    }

    /// <summary>
    /// Slices _allSentiments to only show sentiments matching the visible candle window.
    /// </summary>
    private void UpdateVisibleSentiments()
    {
        lock (_articlesLock)
        {
            if (_allSentiments.Count == 0)
            {
                Sentiments = new ObservableCollection<CandleSentiment>();
                return;
            }

            var startIndex = Math.Max(0, _scrollOffset);
            var endIndex = Math.Min(_allSentiments.Count, startIndex + _visibleCandleCount);

            var visibleSentiments = _allSentiments
                .Skip(startIndex)
                .Take(endIndex - startIndex);

            // Batch update: single property change instead of Clear + N Adds
            Sentiments = new ObservableCollection<CandleSentiment>(visibleSentiments);
        }
    }

    /// <summary>
    /// Gets articles matching a specific candle's time period.
    /// </summary>
    public IEnumerable<NewsArticle> GetArticlesForCandle(Candle candle)
    {
        lock (_articlesLock)
        {
            return _allArticles
                .Where(a => a.PublishedAt >= candle.OpenTime && a.PublishedAt < candle.CloseTime)
                .OrderByDescending(a => a.PublishedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Gets the sentiment data for a specific candle index (within visible range).
    /// </summary>
    public CandleSentiment? GetSentimentAtIndex(int index)
    {
        if (index >= 0 && index < Sentiments.Count)
        {
            return Sentiments[index];
        }
        return null;
    }

    /// <summary>
    /// Sets the highlighted index and updates related properties.
    /// Always updates news panel on hover - pinned candle is only used when mouse leaves chart.
    /// </summary>
    public void SetHighlightedIndex(int index, Candle? candle = null)
    {
        // Always update sentiment chart highlighting
        HighlightedIndex = index;
        HoveredSentiment = GetSentimentAtIndex(index);

        // Update news panel based on hover (pinned candle is fallback when mouse leaves)
        if (candle != null && index >= 0)
        {
            IsShowingCandleSelection = true;
            // Show different header if this is the pinned candle vs just hovering
            SelectionHeaderText = (_pinnedCandle != null && candle.OpenTime == _pinnedCandle.OpenTime)
                ? $"📌 News for {candle.OpenTime:MMM dd, HH:mm}"
                : $"News for {candle.OpenTime:MMM dd, HH:mm}";
            
            // Fire-and-forget: offload filtering to background thread
            // Cancellation handled internally - new calls cancel previous
            _ = UpdateSelectedCandleArticlesAsync(candle);
        }
        else if (index < 0)
        {
            // Mouse left chart - revert to pinned candle if one exists
            RevertToSelectedCandle();
        }
    }

    /// <summary>
    /// Offloads article filtering to a background thread to prevent UI stalls.
    /// Creates the ObservableCollection off-thread (constructor doesn't raise events),
    /// then marshals the assignment to the UI thread.
    /// </summary>
    private async Task UpdateSelectedCandleArticlesAsync(Candle candle)
    {
        // Get token - cancels any previous in-flight filter operation
        var ct = _articleFilterCancellation.GetToken();

        try
        {
            // Capture candle times for closure (avoid capturing entire candle)
            var openTime = candle.OpenTime;
            var closeTime = candle.CloseTime;

            // Do expensive work on background thread:
            // - Lock acquisition and list filtering
            // - LINQ operations
            // - ObservableCollection construction (iterates all items)
            var newCollection = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                List<NewsArticle> filtered;
                lock (_articlesLock)
                {
                    filtered = _allArticles
                        .Where(a => a.PublishedAt >= openTime && a.PublishedAt < closeTime)
                        .OrderByDescending(a => a.PublishedAt)
                        .ToList();
                }

                ct.ThrowIfCancellationRequested();

                // Create collection off-thread - constructor just copies, no events raised
                return new ObservableCollection<NewsArticle>(filtered);
            }, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            // Marshal the assignment to UI thread
            // BeginInvoke is non-blocking, won't stall if UI is busy
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (ct.IsCancellationRequested) return;

                SelectedCandleArticles = newCollection;
                OnPropertyChanged(nameof(DisplayedArticles));
            });
        }
        catch (OperationCanceledException)
        {
            // Expected when a new hover cancels this operation - silently ignore
        }
    }

    /// <summary>
    /// Clears the highlighted state and reverts to showing all visible articles.
    /// </summary>
    public void ClearHighlight()
    {
        HighlightedIndex = -1;
        HoveredSentiment = null;
        HoveredArticle = null;
        ClearCandleSelection();
    }

    /// <summary>
    /// Sets a candle as "selected" (pinned) for persistent news display.
    /// This is called when the user clicks on a candle.
    /// </summary>
    public void SetSelectedCandle(Candle candle)
    {
        _pinnedCandle = candle;
        HasPinnedCandle = true;
        IsShowingCandleSelection = true;
        SelectionHeaderText = $"📌 News for {candle.OpenTime:MMM dd, HH:mm}";
        
        // Fire-and-forget: offload filtering to background thread
        _ = UpdateSelectedCandleArticlesAsync(candle);
    }

    /// <summary>
    /// Clears the selected/pinned candle. Called when user clicks same candle again.
    /// </summary>
    public void ClearSelection()
    {
        _pinnedCandle = null;
        HasPinnedCandle = false;
        ClearCandleSelection();
    }

    /// <summary>
    /// Reverts the news display to the pinned candle after hover ends.
    /// </summary>
    public void RevertToSelectedCandle()
    {
        if (_pinnedCandle != null)
        {
            // Reset the header to show pinned state
            SelectionHeaderText = $"📌 News for {_pinnedCandle.OpenTime:MMM dd, HH:mm}";
            
            // Re-filter to the pinned candle's articles
            _ = UpdateSelectedCandleArticlesAsync(_pinnedCandle);
        }
        else
        {
            ClearCandleSelection();
        }
    }

    private void ClearCandleSelection()
    {
        SelectedCandleArticles = new ObservableCollection<NewsArticle>();
        IsShowingCandleSelection = false;
        SelectionHeaderText = "Market News";
        
        // Force notify DisplayedArticles since we cleared SelectedCandleArticles
        OnPropertyChanged(nameof(DisplayedArticles));
    }

    private void UpdateCounts()
    {
        lock (_articlesLock)
        {
            var visible = _allArticles
                .Where(a => a.PublishedAt >= VisibleStartTime && a.PublishedAt <= VisibleEndTime)
                .ToList();

            TotalBullishCount = visible.Count(a => a.IsBullish);
            TotalBearishCount = visible.Count(a => a.IsBearish);
            TotalNeutralCount = visible.Count(a => a.IsNeutral);
        }
    }

    #endregion

    #region Toggle Methods

    public void TogglePanel()
    {
        IsPanelExpanded = !IsPanelExpanded;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _articleFilterCancellation.Dispose();
    }

    #endregion
}
