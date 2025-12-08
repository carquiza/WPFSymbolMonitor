using System.Windows.Threading;

namespace CryptoChart.App.Infrastructure;

/// <summary>
/// Provides debouncing for async operations with automatic cancellation.
/// When triggered multiple times in quick succession, only the last invocation
/// executes after the debounce delay, and any pending operation is cancelled.
/// </summary>
/// <remarks>
/// Use cases:
/// - Hover events that trigger database lookups
/// - Search-as-you-type functionality  
/// - Resize handlers that recalculate layouts
/// </remarks>
public sealed class AsyncDebouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Dispatcher? _dispatcher;
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _operationCts;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new debouncer with the specified delay.
    /// </summary>
    /// <param name="delay">Time to wait after the last trigger before executing.</param>
    /// <param name="dispatcher">Optional dispatcher for UI thread marshalling.</param>
    public AsyncDebouncer(TimeSpan delay, Dispatcher? dispatcher = null)
    {
        _delay = delay;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Creates a debouncer with delay specified in milliseconds.
    /// </summary>
    public AsyncDebouncer(int delayMilliseconds, Dispatcher? dispatcher = null)
        : this(TimeSpan.FromMilliseconds(delayMilliseconds), dispatcher)
    {
    }

    /// <summary>
    /// Triggers the debounced action. Cancels any pending debounce timer and operation.
    /// The action will execute after the delay if no other trigger occurs.
    /// </summary>
    /// <param name="action">The async action to execute.</param>
    public void Trigger(Func<CancellationToken, Task> action)
    {
        if (_disposed) return;

        lock (_lock)
        {
            // Cancel the previous debounce delay
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();

            // Cancel any running operation from a previous trigger
            _operationCts?.Cancel();
            _operationCts?.Dispose();
            _operationCts = new CancellationTokenSource();

            var debounceCts = _debounceCts;
            var operationCts = _operationCts;

            _ = ExecuteAfterDelayAsync(action, debounceCts.Token, operationCts.Token);
        }
    }

    /// <summary>
    /// Triggers the debounced action with a state parameter to avoid closures.
    /// </summary>
    public void Trigger<TState>(TState state, Func<TState, CancellationToken, Task> action)
    {
        Trigger(ct => action(state, ct));
    }

    /// <summary>
    /// Immediately cancels any pending debounce timer and running operation.
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _debounceCts?.Cancel();
            _operationCts?.Cancel();
        }
    }

    private async Task ExecuteAfterDelayAsync(
        Func<CancellationToken, Task> action,
        CancellationToken debounceToken,
        CancellationToken operationToken)
    {
        try
        {
            // Wait for the debounce delay
            await Task.Delay(_delay, debounceToken).ConfigureAwait(false);

            // If we reach here without cancellation, execute the action
            if (!operationToken.IsCancellationRequested)
            {
                if (_dispatcher != null)
                {
                    // Marshal to UI thread if dispatcher provided
                    await _dispatcher.InvokeAsync(
                        async () => await action(operationToken),
                        DispatcherPriority.Normal,
                        operationToken);
                }
                else
                {
                    await action(operationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when debounce is retriggered or cancelled - ignore
        }
        catch (Exception ex)
        {
            // Log unexpected errors but don't crash
            System.Diagnostics.Debug.WriteLine($"AsyncDebouncer error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _operationCts?.Cancel();
            _operationCts?.Dispose();
        }
    }
}
