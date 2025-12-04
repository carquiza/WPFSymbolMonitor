using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;

namespace CryptoChart.App.ViewModels;

/// <summary>
/// ViewModel for managing news data and sentiment aggregation.
/// Provides synchronized news and sentiment data for the chart.
/// </summary>
public partial class NewsViewModel : ObservableObject
{
    private readonly List<NewsArticle> _allArticles = new();
    private readonly object _articlesLock = new();

    public NewsViewModel()
    {
        VisibleArticles = new ObservableCollection<NewsArticle>();
        Sentiments = new ObservableCollection<CandleSentiment>();
    }

    #region Properties

    [ObservableProperty]
    private ObservableCollection<NewsArticle> _visibleArticles;

    [ObservableProperty]
    private ObservableCollection<CandleSentiment> _sentiments;

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

    public int TotalArticleCount => _allArticles.Count;

    public bool HasArticles => _allArticles.Count > 0;

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
    /// Calculates sentiment aggregation for each candle period.
    /// </summary>
    public void CalculateSentiments(IEnumerable<Candle> candles)
    {
        var candleList = candles.OrderBy(c => c.OpenTime).ToList();

        lock (_articlesLock)
        {
            Sentiments.Clear();

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

                Sentiments.Add(sentiment);
            }
        }
    }

    /// <summary>
    /// Updates visible articles based on the current time range.
    /// </summary>
    public void UpdateVisibleRange(DateTime startTime, DateTime endTime)
    {
        VisibleStartTime = startTime;
        VisibleEndTime = endTime;

        lock (_articlesLock)
        {
            var visible = _allArticles
                .Where(a => a.PublishedAt >= startTime && a.PublishedAt <= endTime)
                .OrderByDescending(a => a.PublishedAt)
                .ToList();

            VisibleArticles.Clear();
            foreach (var article in visible)
            {
                VisibleArticles.Add(article);
            }
        }

        UpdateCounts();
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
    /// Gets the sentiment data for a specific candle index.
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
    /// </summary>
    public void SetHighlightedIndex(int index)
    {
        HighlightedIndex = index;
        HoveredSentiment = GetSentimentAtIndex(index);
    }

    /// <summary>
    /// Clears the highlighted state.
    /// </summary>
    public void ClearHighlight()
    {
        HighlightedIndex = -1;
        HoveredSentiment = null;
        HoveredArticle = null;
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
}
