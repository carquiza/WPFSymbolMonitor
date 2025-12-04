using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CryptoChart.Core.Models;

namespace CryptoChart.App.Controls;

/// <summary>
/// Custom sentiment bar chart control that displays bullish/bearish news counts
/// synchronized with the candlestick chart above it.
/// Bullish bars extend upward (green), bearish bars extend downward (red).
/// </summary>
public class SentimentChart : FrameworkElement
{
    private readonly VisualCollection _visuals;
    private DrawingVisual? _backgroundVisual;
    private DrawingVisual? _barsVisual;
    private DrawingVisual? _highlightVisual;

    #region Constructors

    public SentimentChart()
    {
        _visuals = new VisualCollection(this);

        _backgroundVisual = new DrawingVisual();
        _barsVisual = new DrawingVisual();
        _highlightVisual = new DrawingVisual();

        _visuals.Add(_backgroundVisual);
        _visuals.Add(_barsVisual);
        _visuals.Add(_highlightVisual);

        ClipToBounds = true;
        Loaded += (s, e) => InvalidateChart();
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty SentimentsProperty =
        DependencyProperty.Register(nameof(Sentiments), typeof(IEnumerable<CandleSentiment>),
            typeof(SentimentChart),
            new FrameworkPropertyMetadata(null, OnDataChanged));

    public IEnumerable<CandleSentiment>? Sentiments
    {
        get => (IEnumerable<CandleSentiment>?)GetValue(SentimentsProperty);
        set => SetValue(SentimentsProperty, value);
    }

    public static readonly DependencyProperty HighlightedIndexProperty =
        DependencyProperty.Register(nameof(HighlightedIndex), typeof(int),
            typeof(SentimentChart),
            new FrameworkPropertyMetadata(-1, OnHighlightChanged));

    public int HighlightedIndex
    {
        get => (int)GetValue(HighlightedIndexProperty);
        set => SetValue(HighlightedIndexProperty, value);
    }

    public static readonly DependencyProperty BullishColorProperty =
        DependencyProperty.Register(nameof(BullishColor), typeof(Color),
            typeof(SentimentChart),
            new FrameworkPropertyMetadata(Color.FromRgb(0x26, 0xA6, 0x9A), OnAppearanceChanged));

    public Color BullishColor
    {
        get => (Color)GetValue(BullishColorProperty);
        set => SetValue(BullishColorProperty, value);
    }

    public static readonly DependencyProperty BearishColorProperty =
        DependencyProperty.Register(nameof(BearishColor), typeof(Color),
            typeof(SentimentChart),
            new FrameworkPropertyMetadata(Color.FromRgb(0xEF, 0x53, 0x50), OnAppearanceChanged));

    public Color BearishColor
    {
        get => (Color)GetValue(BearishColorProperty);
        set => SetValue(BearishColorProperty, value);
    }

    public static readonly DependencyProperty ChartBackgroundProperty =
        DependencyProperty.Register(nameof(ChartBackground), typeof(Color),
            typeof(SentimentChart),
            new FrameworkPropertyMetadata(Color.FromRgb(0x0D, 0x11, 0x17), OnAppearanceChanged));

    public Color ChartBackground
    {
        get => (Color)GetValue(ChartBackgroundProperty);
        set => SetValue(ChartBackgroundProperty, value);
    }

    public static readonly DependencyProperty GridColorProperty =
        DependencyProperty.Register(nameof(GridColor), typeof(Color),
            typeof(SentimentChart),
            new FrameworkPropertyMetadata(Color.FromRgb(0x21, 0x26, 0x2D), OnAppearanceChanged));

    public Color GridColor
    {
        get => (Color)GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    #endregion

    #region Visual Tree Overrides

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index)
    {
        if (index < 0 || index >= _visuals.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _visuals[index];
    }

    #endregion

    #region Property Change Handlers

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SentimentChart)d).InvalidateChart();
    }

    private static void OnHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SentimentChart)d).DrawHighlight();
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SentimentChart)d).InvalidateChart();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateChart();
    }

    #endregion

    #region Constants

    private const double LeftMargin = 60;   // Match candlestick chart
    private const double RightMargin = 10;
    private const double TopMargin = 4;
    private const double BottomMargin = 4;

    private double ChartWidth => Math.Max(0, ActualWidth - LeftMargin - RightMargin);
    private double ChartHeight => Math.Max(0, ActualHeight - TopMargin - BottomMargin);
    private double CenterY => TopMargin + ChartHeight / 2;

    #endregion

    #region Rendering

    public void InvalidateChart()
    {
        DrawBackground();
        DrawBars();
        DrawHighlight();
    }

    private void DrawBackground()
    {
        if (_backgroundVisual == null) return;

        using var dc = _backgroundVisual.RenderOpen();

        // Fill background
        var bgBrush = new SolidColorBrush(ChartBackground);
        bgBrush.Freeze();
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // Draw center line (zero line)
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1);
        gridPen.Freeze();
        dc.DrawLine(gridPen, new Point(LeftMargin, CenterY), new Point(ActualWidth - RightMargin, CenterY));

        // Draw border
        var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)), 1);
        borderPen.Freeze();
        dc.DrawRectangle(null, borderPen, new Rect(LeftMargin, TopMargin, ChartWidth, ChartHeight));

        // Draw label
        var textBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
        textBrush.Freeze();
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var labelText = new FormattedText(
            "News",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            10,
            textBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(labelText, new Point(5, CenterY - labelText.Height / 2));
    }

    private void DrawBars()
    {
        if (_barsVisual == null || Sentiments == null || ChartWidth <= 0 || ChartHeight <= 0) return;

        using var dc = _barsVisual.RenderOpen();

        var sentimentList = Sentiments.ToList();
        if (sentimentList.Count == 0) return;

        // Calculate max count for scaling
        var maxCount = sentimentList.Max(s => Math.Max(s.BullishCount, s.BearishCount));
        if (maxCount == 0) maxCount = 1;

        // Brushes
        var bullishBrush = new SolidColorBrush(BullishColor);
        bullishBrush.Freeze();
        var bearishBrush = new SolidColorBrush(BearishColor);
        bearishBrush.Freeze();

        var barWidth = Math.Max(2, (ChartWidth / sentimentList.Count) - 2);
        var halfHeight = ChartHeight / 2 - 2; // Leave some padding

        for (int i = 0; i < sentimentList.Count; i++)
        {
            var sentiment = sentimentList[i];
            var x = LeftMargin + (i * (ChartWidth / sentimentList.Count)) + ((ChartWidth / sentimentList.Count) / 2);

            // Draw bullish bar (upward from center)
            if (sentiment.BullishCount > 0)
            {
                var barHeight = (sentiment.BullishCount / (double)maxCount) * halfHeight;
                var rect = new Rect(x - barWidth / 2, CenterY - barHeight, barWidth, barHeight);
                dc.DrawRectangle(bullishBrush, null, rect);
            }

            // Draw bearish bar (downward from center)
            if (sentiment.BearishCount > 0)
            {
                var barHeight = (sentiment.BearishCount / (double)maxCount) * halfHeight;
                var rect = new Rect(x - barWidth / 2, CenterY, barWidth, barHeight);
                dc.DrawRectangle(bearishBrush, null, rect);
            }
        }
    }

    private void DrawHighlight()
    {
        if (_highlightVisual == null) return;

        using var dc = _highlightVisual.RenderOpen();

        if (HighlightedIndex < 0 || Sentiments == null) return;

        var sentimentList = Sentiments.ToList();
        if (HighlightedIndex >= sentimentList.Count) return;

        var x = LeftMargin + (HighlightedIndex * (ChartWidth / sentimentList.Count)) + ((ChartWidth / sentimentList.Count) / 2);
        var highlightWidth = ChartWidth / sentimentList.Count;

        // Draw highlight rectangle
        var highlightBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
        highlightBrush.Freeze();
        var rect = new Rect(x - highlightWidth / 2, TopMargin, highlightWidth, ChartHeight);
        dc.DrawRectangle(highlightBrush, null, rect);
    }

    #endregion
}
