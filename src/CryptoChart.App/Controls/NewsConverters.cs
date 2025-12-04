using System.Globalization;
using System.Windows.Data;

namespace CryptoChart.App.Controls;

/// <summary>
/// Converts a boolean to a width value for panel expansion/collapse.
/// </summary>
public class BoolToWidthConverter : IValueConverter
{
    public double ExpandedWidth { get; set; } = 280;
    public double CollapsedWidth { get; set; } = 0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? ExpandedWidth : CollapsedWidth;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts sentiment type to appropriate color.
/// </summary>
public class SentimentToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string sentiment)
        {
            return sentiment switch
            {
                "Bullish" => "#26A69A",
                "Bearish" => "#EF5350",
                _ => "#8B949E"
            };
        }
        return "#8B949E";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
