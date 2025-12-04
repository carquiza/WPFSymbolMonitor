using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CryptoChart.Core.Models;

namespace CryptoChart.App.Controls;

/// <summary>
/// High-performance custom candlestick chart control using DrawingVisual.
/// Supports pan, zoom, crosshair, and real-time updates.
/// </summary>
public class CandlestickChart : FrameworkElement
{
    private readonly VisualCollection _visuals;
    private DrawingVisual? _chartVisual;
    private DrawingVisual? _gridVisual;
    private DrawingVisual? _crosshairVisual;
    private DrawingVisual? _tooltipVisual;

    private bool _isDragging;
    private Point _lastDragPoint;

    #region Constructors

    public CandlestickChart()
    {
        _visuals = new VisualCollection(this);

        // Create visual layers (order matters for z-index)
        _gridVisual = new DrawingVisual();
        _chartVisual = new DrawingVisual();
        _crosshairVisual = new DrawingVisual();
        _tooltipVisual = new DrawingVisual();

        _visuals.Add(_gridVisual);
        _visuals.Add(_chartVisual);
        _visuals.Add(_crosshairVisual);
        _visuals.Add(_tooltipVisual);

        // Enable mouse events
        ClipToBounds = true;
        Loaded += OnLoaded;
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty CandlesProperty =
        DependencyProperty.Register(nameof(Candles), typeof(IEnumerable<Candle>),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(null, OnCandlesChanged));

    public IEnumerable<Candle>? Candles
    {
        get => (IEnumerable<Candle>?)GetValue(CandlesProperty);
        set => SetValue(CandlesProperty, value);
    }

    public static readonly DependencyProperty MinPriceProperty =
        DependencyProperty.Register(nameof(MinPrice), typeof(decimal),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(0m, OnPriceRangeChanged));

    public decimal MinPrice
    {
        get => (decimal)GetValue(MinPriceProperty);
        set => SetValue(MinPriceProperty, value);
    }

    public static readonly DependencyProperty MaxPriceProperty =
        DependencyProperty.Register(nameof(MaxPrice), typeof(decimal),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(0m, OnPriceRangeChanged));

    public decimal MaxPrice
    {
        get => (decimal)GetValue(MaxPriceProperty);
        set => SetValue(MaxPriceProperty, value);
    }

    public static readonly DependencyProperty BullishColorProperty =
        DependencyProperty.Register(nameof(BullishColor), typeof(Color),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(Color.FromRgb(0x26, 0xA6, 0x9A), OnAppearanceChanged));

    public Color BullishColor
    {
        get => (Color)GetValue(BullishColorProperty);
        set => SetValue(BullishColorProperty, value);
    }

    public static readonly DependencyProperty BearishColorProperty =
        DependencyProperty.Register(nameof(BearishColor), typeof(Color),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(Color.FromRgb(0xEF, 0x53, 0x50), OnAppearanceChanged));

    public Color BearishColor
    {
        get => (Color)GetValue(BearishColorProperty);
        set => SetValue(BearishColorProperty, value);
    }

    public static readonly DependencyProperty GridColorProperty =
        DependencyProperty.Register(nameof(GridColor), typeof(Color),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(Color.FromRgb(0x21, 0x26, 0x2D), OnAppearanceChanged));

    public Color GridColor
    {
        get => (Color)GetValue(GridColorProperty);
        set => SetValue(GridColorProperty, value);
    }

    public static readonly DependencyProperty ChartBackgroundProperty =
        DependencyProperty.Register(nameof(ChartBackground), typeof(Color),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(Color.FromRgb(0x0D, 0x11, 0x17), OnAppearanceChanged));

    public Color ChartBackground
    {
        get => (Color)GetValue(ChartBackgroundProperty);
        set => SetValue(ChartBackgroundProperty, value);
    }

    public static readonly DependencyProperty HoveredCandleProperty =
        DependencyProperty.Register(nameof(HoveredCandle), typeof(Candle),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(null));

    public Candle? HoveredCandle
    {
        get => (Candle?)GetValue(HoveredCandleProperty);
        set => SetValue(HoveredCandleProperty, value);
    }

    public static readonly DependencyProperty ShowCrosshairProperty =
        DependencyProperty.Register(nameof(ShowCrosshair), typeof(bool),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(false, OnCrosshairChanged));

    public bool ShowCrosshair
    {
        get => (bool)GetValue(ShowCrosshairProperty);
        set => SetValue(ShowCrosshairProperty, value);
    }

    public static readonly DependencyProperty HoveredSentimentProperty =
        DependencyProperty.Register(nameof(HoveredSentiment), typeof(CandleSentiment),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(null, OnCrosshairChanged));

    public CandleSentiment? HoveredSentiment
    {
        get => (CandleSentiment?)GetValue(HoveredSentimentProperty);
        set => SetValue(HoveredSentimentProperty, value);
    }

    public static readonly DependencyProperty CrosshairXProperty =
        DependencyProperty.Register(nameof(CrosshairX), typeof(double),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(0.0, OnCrosshairChanged));

    public double CrosshairX
    {
        get => (double)GetValue(CrosshairXProperty);
        set => SetValue(CrosshairXProperty, value);
    }

    public static readonly DependencyProperty CrosshairYProperty =
        DependencyProperty.Register(nameof(CrosshairY), typeof(double),
            typeof(CandlestickChart),
            new FrameworkPropertyMetadata(0.0, OnCrosshairChanged));

    public double CrosshairY
    {
        get => (double)GetValue(CrosshairYProperty);
        set => SetValue(CrosshairYProperty, value);
    }

    #endregion

    #region Routed Events

    public static readonly RoutedEvent ScrollRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(ScrollRequested), RoutingStrategy.Bubble,
            typeof(EventHandler<ScrollEventArgs>), typeof(CandlestickChart));

    public event EventHandler<ScrollEventArgs> ScrollRequested
    {
        add => AddHandler(ScrollRequestedEvent, value);
        remove => RemoveHandler(ScrollRequestedEvent, value);
    }

    public static readonly RoutedEvent ZoomRequestedEvent =
        EventManager.RegisterRoutedEvent(nameof(ZoomRequested), RoutingStrategy.Bubble,
            typeof(EventHandler<ZoomEventArgs>), typeof(CandlestickChart));

    public event EventHandler<ZoomEventArgs> ZoomRequested
    {
        add => AddHandler(ZoomRequestedEvent, value);
        remove => RemoveHandler(ZoomRequestedEvent, value);
    }

    public static readonly RoutedEvent CandleHoveredEvent =
        EventManager.RegisterRoutedEvent(nameof(CandleHovered), RoutingStrategy.Bubble,
            typeof(EventHandler<CandleHoveredEventArgs>), typeof(CandlestickChart));

    public event EventHandler<CandleHoveredEventArgs> CandleHovered
    {
        add => AddHandler(CandleHoveredEvent, value);
        remove => RemoveHandler(CandleHoveredEvent, value);
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

    #region Layout & Rendering

    private const double LeftMargin = 60;   // Price axis
    private const double RightMargin = 10;
    private const double TopMargin = 10;
    private const double BottomMargin = 30; // Time axis

    private double ChartWidth => Math.Max(0, ActualWidth - LeftMargin - RightMargin);
    private double ChartHeight => Math.Max(0, ActualHeight - TopMargin - BottomMargin);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InvalidateChart();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateChart();
    }

    private static void OnCandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CandlestickChart)d).InvalidateChart();
    }

    private static void OnPriceRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CandlestickChart)d).InvalidateChart();
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CandlestickChart)d).InvalidateChart();
    }

    private static void OnCrosshairChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((CandlestickChart)d).DrawCrosshair();
    }

    public void InvalidateChart()
    {
        DrawBackground();
        DrawGrid();
        DrawCandles();
        DrawCrosshair();
    }

    private void DrawBackground()
    {
        if (_gridVisual == null) return;

        using var dc = _gridVisual.RenderOpen();
        
        // Fill background
        var bgBrush = new SolidColorBrush(ChartBackground);
        bgBrush.Freeze();
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));
    }

    private void DrawGrid()
    {
        if (_gridVisual == null || ChartWidth <= 0 || ChartHeight <= 0) return;

        using var dc = _gridVisual.RenderOpen();

        // Draw background first
        var bgBrush = new SolidColorBrush(ChartBackground);
        bgBrush.Freeze();
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var gridPen = new Pen(new SolidColorBrush(GridColor), 1);
        gridPen.Freeze();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
        textBrush.Freeze();

        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // Draw horizontal grid lines and price labels
        if (MaxPrice > MinPrice)
        {
            var priceRange = MaxPrice - MinPrice;
            var gridLines = 6;
            var priceStep = priceRange / gridLines;

            for (int i = 0; i <= gridLines; i++)
            {
                var price = MinPrice + (priceStep * i);
                var y = PriceToY(price);

                // Grid line
                dc.DrawLine(gridPen, new Point(LeftMargin, y), new Point(ActualWidth - RightMargin, y));

                // Price label
                var priceText = FormatPrice(price);
                var formattedText = new FormattedText(
                    priceText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    11,
                    textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(formattedText, new Point(5, y - formattedText.Height / 2));
            }
        }

        // Draw border around chart area
        var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)), 1);
        borderPen.Freeze();
        dc.DrawRectangle(null, borderPen, 
            new Rect(LeftMargin, TopMargin, ChartWidth, ChartHeight));
    }

    private void DrawCandles()
    {
        if (_chartVisual == null || Candles == null || ChartWidth <= 0 || ChartHeight <= 0) return;

        using var dc = _chartVisual.RenderOpen();

        var candleList = Candles.ToList();
        if (candleList.Count == 0) return;

        var bullishBrush = new SolidColorBrush(BullishColor);
        bullishBrush.Freeze();
        var bearishBrush = new SolidColorBrush(BearishColor);
        bearishBrush.Freeze();

        var candleWidth = Math.Max(2, (ChartWidth / candleList.Count) - 2);
        var wickWidth = Math.Max(1, candleWidth / 6);

        var textBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
        textBrush.Freeze();
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        for (int i = 0; i < candleList.Count; i++)
        {
            var candle = candleList[i];
            var brush = candle.IsBullish ? bullishBrush : bearishBrush;
            var pen = new Pen(brush, 1);
            pen.Freeze();

            var x = LeftMargin + (i * (ChartWidth / candleList.Count)) + ((ChartWidth / candleList.Count) / 2);

            var highY = PriceToY(candle.High);
            var lowY = PriceToY(candle.Low);
            var openY = PriceToY(candle.Open);
            var closeY = PriceToY(candle.Close);

            // Draw wick
            dc.DrawLine(pen, new Point(x, highY), new Point(x, lowY));

            // Draw body
            var bodyTop = Math.Min(openY, closeY);
            var bodyHeight = Math.Max(1, Math.Abs(openY - closeY));
            var bodyRect = new Rect(x - candleWidth / 2, bodyTop, candleWidth, bodyHeight);

            dc.DrawRectangle(brush, null, bodyRect);

            // Draw time labels (every N candles)
            if (i % Math.Max(1, candleList.Count / 6) == 0)
            {
                var timeText = candle.OpenTime.ToString("MM/dd");
                var formattedText = new FormattedText(
                    timeText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    10,
                    textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(formattedText, 
                    new Point(x - formattedText.Width / 2, ActualHeight - BottomMargin + 8));
            }
        }
    }

    private void DrawCrosshair()
    {
        if (_crosshairVisual == null) return;

        using var dc = _crosshairVisual.RenderOpen();

        if (!ShowCrosshair || CrosshairX <= LeftMargin || CrosshairX >= ActualWidth - RightMargin)
            return;

        var crosshairPen = new Pen(new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)), 1)
        {
            DashStyle = DashStyles.Dash
        };
        crosshairPen.Freeze();

        // Vertical line
        dc.DrawLine(crosshairPen, 
            new Point(CrosshairX, TopMargin), 
            new Point(CrosshairX, ActualHeight - BottomMargin));

        // Horizontal line
        if (CrosshairY >= TopMargin && CrosshairY <= ActualHeight - BottomMargin)
        {
            dc.DrawLine(crosshairPen, 
                new Point(LeftMargin, CrosshairY), 
                new Point(ActualWidth - RightMargin, CrosshairY));

            // Price label at crosshair
            var price = YToPrice(CrosshairY);
            DrawPriceLabel(dc, price, CrosshairY);
        }

        // Draw tooltip if we have a hovered candle
        if (HoveredCandle != null)
        {
            DrawTooltip(dc);
        }
    }

    private void DrawPriceLabel(DrawingContext dc, decimal price, double y)
    {
        var textBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3));
        textBrush.Freeze();
        var bgBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));
        bgBrush.Freeze();

        var typeface = new Typeface(new FontFamily("Cascadia Mono"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var priceText = FormatPrice(price);
        var formattedText = new FormattedText(
            priceText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            11,
            textBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var labelRect = new Rect(2, y - formattedText.Height / 2 - 2, 
            formattedText.Width + 6, formattedText.Height + 4);
        dc.DrawRectangle(bgBrush, null, labelRect);
        dc.DrawText(formattedText, new Point(5, y - formattedText.Height / 2));
    }

    private void DrawTooltip(DrawingContext dc)
    {
        if (HoveredCandle == null) return;

        var textBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3));
        textBrush.Freeze();
        var secondaryBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));
        secondaryBrush.Freeze();
        var bgBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22));
        bgBrush.Freeze();
        var borderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));
        borderBrush.Freeze();
        var bullishBrush = new SolidColorBrush(BullishColor);
        bullishBrush.Freeze();
        var bearishBrush = new SolidColorBrush(BearishColor);
        bearishBrush.Freeze();

        var typeface = new Typeface(new FontFamily("Cascadia Mono"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // Build tooltip lines
        var lines = new List<(string text, Brush brush)>
        {
            ($"O: {FormatPrice(HoveredCandle.Open)}", textBrush),
            ($"H: {FormatPrice(HoveredCandle.High)}", textBrush),
            ($"L: {FormatPrice(HoveredCandle.Low)}", textBrush),
            ($"C: {FormatPrice(HoveredCandle.Close)}", textBrush),
            ($"V: {HoveredCandle.Volume:N0}", secondaryBrush)
        };

        // Add sentiment info if available
        if (HoveredSentiment != null && HoveredSentiment.HasNews)
        {
            lines.Add(("", secondaryBrush)); // Spacer
            if (HoveredSentiment.BullishCount > 0)
                lines.Add(($"▲ {HoveredSentiment.BullishCount} Bullish", bullishBrush));
            if (HoveredSentiment.BearishCount > 0)
                lines.Add(($"▼ {HoveredSentiment.BearishCount} Bearish", bearishBrush));
            if (HoveredSentiment.NeutralCount > 0)
                lines.Add(($"● {HoveredSentiment.NeutralCount} Neutral", secondaryBrush));
        }

        var maxWidth = 0.0;
        var lineHeight = 16.0;
        var padding = 8.0;

        foreach (var (lineText, _) in lines)
        {
            if (string.IsNullOrEmpty(lineText)) continue;
            var text = new FormattedText(
                lineText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, 11, textBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            maxWidth = Math.Max(maxWidth, text.Width);
        }

        var tooltipWidth = maxWidth + padding * 2;
        var tooltipHeight = lines.Count * lineHeight + padding * 2;
        var tooltipX = Math.Min(CrosshairX + 15, ActualWidth - tooltipWidth - 10);
        var tooltipY = Math.Max(TopMargin, Math.Min(CrosshairY - tooltipHeight / 2, ActualHeight - BottomMargin - tooltipHeight));

        // Draw background
        var tooltipRect = new Rect(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
        dc.DrawRoundedRectangle(bgBrush, new Pen(borderBrush, 1), tooltipRect, 4, 4);

        // Draw text lines
        var y = tooltipY + padding;
        foreach (var (lineText, lineBrush) in lines)
        {
            if (!string.IsNullOrEmpty(lineText))
            {
                var text = new FormattedText(
                    lineText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 11, lineBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(text, new Point(tooltipX + padding, y));
            }
            y += lineHeight;
        }

        // Draw date header
        var dateText = new FormattedText(
            HoveredCandle.OpenTime.ToString("yyyy-MM-dd HH:mm"),
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, 10, secondaryBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(dateText, new Point(tooltipX + padding, tooltipY - 16));
    }

    #endregion

    #region Coordinate Conversion

    private double PriceToY(decimal price)
    {
        if (MaxPrice == MinPrice) return TopMargin + ChartHeight / 2;
        var normalized = (double)((price - MinPrice) / (MaxPrice - MinPrice));
        return ActualHeight - BottomMargin - (normalized * ChartHeight);
    }

    private decimal YToPrice(double y)
    {
        if (ChartHeight <= 0) return 0;
        var normalized = (ActualHeight - BottomMargin - y) / ChartHeight;
        return MinPrice + (decimal)normalized * (MaxPrice - MinPrice);
    }

    private int XToCandleIndex(double x)
    {
        if (Candles == null) return -1;
        var candleCount = Candles.Count();
        if (candleCount == 0) return -1;

        var normalizedX = (x - LeftMargin) / ChartWidth;
        var index = (int)(normalizedX * candleCount);
        return Math.Clamp(index, 0, candleCount - 1);
    }

    #endregion

    #region Mouse Interaction

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var pos = e.GetPosition(this);

        if (_isDragging)
        {
            var delta = (int)((pos.X - _lastDragPoint.X) / 10);
            if (delta != 0)
            {
                RaiseEvent(new ScrollEventArgs(ScrollRequestedEvent, this, -delta));
                _lastDragPoint = pos;
            }
        }
        else
        {
            CrosshairX = pos.X;
            CrosshairY = pos.Y;
            ShowCrosshair = true;

            var candleIndex = XToCandleIndex(pos.X);
            RaiseEvent(new CandleHoveredEventArgs(CandleHoveredEvent, this, candleIndex));
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ShowCrosshair = false;
        _isDragging = false;
        RaiseEvent(new CandleHoveredEventArgs(CandleHoveredEvent, this, -1));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _isDragging = true;
        _lastDragPoint = e.GetPosition(this);
        CaptureMouse();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _isDragging = false;
        ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var delta = e.Delta > 0 ? -10 : 10;
        RaiseEvent(new ZoomEventArgs(ZoomRequestedEvent, this, delta));
    }

    #endregion

    #region Helpers

    private static string FormatPrice(decimal price)
    {
        return price switch
        {
            >= 10000 => price.ToString("N0"),
            >= 1000 => price.ToString("N1"),
            >= 100 => price.ToString("N2"),
            >= 1 => price.ToString("N4"),
            >= 0.01m => price.ToString("N6"),
            _ => price.ToString("N8")
        };
    }

    #endregion
}

#region Event Args

public class ScrollEventArgs : RoutedEventArgs
{
    public int Delta { get; }

    public ScrollEventArgs(RoutedEvent routedEvent, object source, int delta) 
        : base(routedEvent, source)
    {
        Delta = delta;
    }
}

public class ZoomEventArgs : RoutedEventArgs
{
    public int Delta { get; }

    public ZoomEventArgs(RoutedEvent routedEvent, object source, int delta) 
        : base(routedEvent, source)
    {
        Delta = delta;
    }
}

public class CandleHoveredEventArgs : RoutedEventArgs
{
    public int CandleIndex { get; }

    public CandleHoveredEventArgs(RoutedEvent routedEvent, object source, int candleIndex) 
        : base(routedEvent, source)
    {
        CandleIndex = candleIndex;
    }
}

#endregion
