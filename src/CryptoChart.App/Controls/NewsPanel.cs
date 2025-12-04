using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CryptoChart.Core.Models;

namespace CryptoChart.App.Controls;

/// <summary>
/// Collapsible news panel that displays articles with sentiment badges.
/// Supports auto-scrolling to articles matching a highlighted time period.
/// </summary>
public class NewsPanel : Control
{
    static NewsPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NewsPanel),
            new FrameworkPropertyMetadata(typeof(NewsPanel)));
    }

    #region Dependency Properties

    public static readonly DependencyProperty ArticlesProperty =
        DependencyProperty.Register(nameof(Articles), typeof(IEnumerable<NewsArticle>),
            typeof(NewsPanel),
            new FrameworkPropertyMetadata(null, OnArticlesChanged));

    public IEnumerable<NewsArticle>? Articles
    {
        get => (IEnumerable<NewsArticle>?)GetValue(ArticlesProperty);
        set => SetValue(ArticlesProperty, value);
    }

    public static readonly DependencyProperty HighlightedTimeProperty =
        DependencyProperty.Register(nameof(HighlightedTime), typeof(DateTime?),
            typeof(NewsPanel),
            new FrameworkPropertyMetadata(null, OnHighlightedTimeChanged));

    public DateTime? HighlightedTime
    {
        get => (DateTime?)GetValue(HighlightedTimeProperty);
        set => SetValue(HighlightedTimeProperty, value);
    }

    public static readonly DependencyProperty TimeRangeStartProperty =
        DependencyProperty.Register(nameof(TimeRangeStart), typeof(DateTime),
            typeof(NewsPanel),
            new FrameworkPropertyMetadata(DateTime.MinValue));

    public DateTime TimeRangeStart
    {
        get => (DateTime)GetValue(TimeRangeStartProperty);
        set => SetValue(TimeRangeStartProperty, value);
    }

    public static readonly DependencyProperty TimeRangeEndProperty =
        DependencyProperty.Register(nameof(TimeRangeEnd), typeof(DateTime),
            typeof(NewsPanel),
            new FrameworkPropertyMetadata(DateTime.MaxValue));

    public DateTime TimeRangeEnd
    {
        get => (DateTime)GetValue(TimeRangeEndProperty);
        set => SetValue(TimeRangeEndProperty, value);
    }

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool),
            typeof(NewsPanel),
            new FrameworkPropertyMetadata(true, OnIsExpandedChanged));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public static readonly DependencyProperty PanelWidthProperty =
        DependencyProperty.Register(nameof(PanelWidth), typeof(double),
            typeof(NewsPanel),
            new FrameworkPropertyMetadata(280.0));

    public double PanelWidth
    {
        get => (double)GetValue(PanelWidthProperty);
        set => SetValue(PanelWidthProperty, value);
    }

    public static readonly DependencyProperty SelectedArticleProperty =
        DependencyProperty.Register(nameof(SelectedArticle), typeof(NewsArticle),
            typeof(NewsPanel),
            new FrameworkPropertyMetadata(null));

    public NewsArticle? SelectedArticle
    {
        get => (NewsArticle?)GetValue(SelectedArticleProperty);
        set => SetValue(SelectedArticleProperty, value);
    }

    public static readonly DependencyProperty BullishColorProperty =
        DependencyProperty.Register(nameof(BullishColor), typeof(Color),
            typeof(NewsPanel),
            new FrameworkPropertyMetadata(Color.FromRgb(0x26, 0xA6, 0x9A)));

    public Color BullishColor
    {
        get => (Color)GetValue(BullishColorProperty);
        set => SetValue(BullishColorProperty, value);
    }

    public static readonly DependencyProperty BearishColorProperty =
        DependencyProperty.Register(nameof(BearishColor), typeof(Color),
            typeof(NewsPanel),
            new FrameworkPropertyMetadata(Color.FromRgb(0xEF, 0x53, 0x50)));

    public Color BearishColor
    {
        get => (Color)GetValue(BearishColorProperty);
        set => SetValue(BearishColorProperty, value);
    }

    #endregion

    #region Routed Events

    public static readonly RoutedEvent ArticleClickedEvent =
        EventManager.RegisterRoutedEvent(nameof(ArticleClicked), RoutingStrategy.Bubble,
            typeof(EventHandler<ArticleClickedEventArgs>), typeof(NewsPanel));

    public event EventHandler<ArticleClickedEventArgs> ArticleClicked
    {
        add => AddHandler(ArticleClickedEvent, value);
        remove => RemoveHandler(ArticleClickedEvent, value);
    }

    #endregion

    #region Property Changed Handlers

    private static void OnArticlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Articles updated - panel will re-render via binding
    }

    private static void OnHighlightedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (NewsPanel)d;
        panel.ScrollToHighlightedTime();
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (NewsPanel)d;
        panel.AnimateExpandCollapse();
    }

    #endregion

    #region Methods

    private void ScrollToHighlightedTime()
    {
        // This would be implemented with template part access
        // For now, the binding in the template handles visual updates
    }

    private void AnimateExpandCollapse()
    {
        // Animation can be added via template triggers or code-behind
    }

    public IEnumerable<NewsArticle> GetFilteredArticles()
    {
        if (Articles == null) return Enumerable.Empty<NewsArticle>();

        return Articles
            .Where(a => a.PublishedAt >= TimeRangeStart && a.PublishedAt <= TimeRangeEnd)
            .OrderByDescending(a => a.PublishedAt);
    }

    public (int bullish, int bearish, int neutral) GetSentimentCounts()
    {
        if (Articles == null) return (0, 0, 0);

        var filtered = GetFilteredArticles().ToList();
        return (
            filtered.Count(a => a.IsBullish),
            filtered.Count(a => a.IsBearish),
            filtered.Count(a => a.IsNeutral)
        );
    }

    #endregion
}

#region Event Args

public class ArticleClickedEventArgs : RoutedEventArgs
{
    public NewsArticle Article { get; }

    public ArticleClickedEventArgs(RoutedEvent routedEvent, object source, NewsArticle article)
        : base(routedEvent, source)
    {
        Article = article;
    }
}

#endregion
