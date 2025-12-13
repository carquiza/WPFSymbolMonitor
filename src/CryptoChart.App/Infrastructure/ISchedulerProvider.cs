using System.Reactive.Concurrency;

namespace CryptoChart.App.Infrastructure;

/// <summary>
/// Abstraction for Rx schedulers to enable unit testing.
/// In tests, use TestScheduler for deterministic timing control.
/// </summary>
public interface ISchedulerProvider
{
    /// <summary>
    /// Scheduler for UI thread operations (WPF Dispatcher).
    /// </summary>
    IScheduler MainThread { get; }

    /// <summary>
    /// Scheduler for background/thread pool operations.
    /// </summary>
    IScheduler Background { get; }

    /// <summary>
    /// Scheduler for immediate execution (synchronous).
    /// Useful for testing without delays.
    /// </summary>
    IScheduler Immediate { get; }
}
