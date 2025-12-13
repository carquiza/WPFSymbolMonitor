using System.Reactive.Concurrency;
using System.Windows;

namespace CryptoChart.App.Infrastructure;

/// <summary>
/// Production scheduler provider using WPF SynchronizationContext and ThreadPool.
/// </summary>
public class SchedulerProvider : ISchedulerProvider
{
    private readonly Lazy<IScheduler> _mainThread;

    public SchedulerProvider()
    {
        // Lazy init to capture the UI SynchronizationContext
        // Must be created on the UI thread (which it will be via DI in App.xaml.cs)
        _mainThread = new Lazy<IScheduler>(() =>
        {
            // Use SynchronizationContext which works with WPF's DispatcherSynchronizationContext
            var context = SynchronizationContext.Current;
            if (context == null)
            {
                // Fallback: create from current dispatcher
                var dispatcher = Application.Current?.Dispatcher 
                    ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
                
                // Ensure we have a synchronization context
                dispatcher.Invoke(() => { });
                context = SynchronizationContext.Current ?? new SynchronizationContext();
            }
            
            return new SynchronizationContextScheduler(context);
        });
    }

    /// <inheritdoc />
    public IScheduler MainThread => _mainThread.Value;

    /// <inheritdoc />
    public IScheduler Background => ThreadPoolScheduler.Instance;

    /// <inheritdoc />
    public IScheduler Immediate => ImmediateScheduler.Instance;
}
