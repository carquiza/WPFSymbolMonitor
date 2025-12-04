using System.Windows;
using CryptoChart.App.Controls;
using CryptoChart.App.ViewModels;

namespace CryptoChart.App.Views;

/// <summary>
/// Main window for the crypto chart application.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadSymbolsCommand.ExecuteAsync(null);
    }

    private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
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

    private void OnCandleHovered(object sender, CandleHoveredEventArgs e)
    {
        ViewModel.ChartViewModel.SetHoveredCandle(e.CandleIndex);
        
        // Synchronize sentiment chart highlight
        if (ViewModel.NewsViewModel != null)
        {
            ViewModel.NewsViewModel.SetHighlightedIndex(e.CandleIndex);
        }
    }
}
