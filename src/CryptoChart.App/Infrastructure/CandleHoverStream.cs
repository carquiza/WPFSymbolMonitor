using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CryptoChart.App.Infrastructure;

/// <summary>
/// Reactive stream for candle hover events with conflation.
/// Throttles rapid mouse movements to prevent UI stalls.
/// </summary>
/// <remarks>
/// Key Rx operators used:
/// - Throttle: Only emit after a quiet period (conflation)
/// - DistinctUntilChanged: Skip consecutive duplicate indices
/// - ObserveOn: Ensure delivery on UI thread
/// </remarks>
public sealed class CandleHoverStream : IDisposable
{
    private readonly Subject<int> _hoverSubject = new();
    private readonly IObservable<int> _throttledStream;
    private readonly CompositeDisposable _disposables = new();
    private bool _disposed;

    /// <summary>
    /// Default throttle duration in milliseconds.
    /// 50ms = max 20 updates/second, balances responsiveness vs performance.
    /// </summary>
    public const int DefaultThrottleMs = 50;

    /// <summary>
    /// Creates a new hover stream with the specified scheduler provider.
    /// </summary>
    /// <param name="schedulerProvider">Provider for thread schedulers.</param>
    /// <param name="throttleMs">Throttle duration in milliseconds. Default is 50ms.</param>
    public CandleHoverStream(ISchedulerProvider schedulerProvider, int throttleMs = DefaultThrottleMs)
    {
        ArgumentNullException.ThrowIfNull(schedulerProvider);

        // Build the reactive pipeline:
        // 1. Throttle: Wait for quiet period before emitting (conflation)
        //    - If user moves mouse rapidly, only the last position after pause matters
        // 2. DistinctUntilChanged: Don't emit if same candle index as before
        //    - Prevents redundant updates when mouse stays over same candle
        // 3. ObserveOn: Ensure subscriber receives on UI thread
        //    - Safe for WPF binding updates
        _throttledStream = _hoverSubject
            .Throttle(TimeSpan.FromMilliseconds(throttleMs), schedulerProvider.Background)
            .DistinctUntilChanged()
            .ObserveOn(schedulerProvider.MainThread);

        _disposables.Add(_hoverSubject);
    }

    /// <summary>
    /// The throttled and deduplicated hover index stream.
    /// Subscribe to receive conflated hover updates.
    /// </summary>
    public IObservable<int> HoverIndex => _throttledStream;

    /// <summary>
    /// Push a new hover index into the stream.
    /// Call this from mouse move events.
    /// </summary>
    /// <param name="candleIndex">The index of the hovered candle, or -1 if none.</param>
    public void OnHover(int candleIndex)
    {
        if (_disposed) return;
        _hoverSubject.OnNext(candleIndex);
    }

    /// <summary>
    /// Signal that hovering has ended (mouse left the chart).
    /// </summary>
    public void OnLeave()
    {
        if (_disposed) return;
        _hoverSubject.OnNext(-1);
    }

    /// <summary>
    /// Subscribe to the throttled hover stream.
    /// </summary>
    /// <param name="onNext">Action to invoke with each throttled hover index.</param>
    /// <returns>Disposable subscription. Dispose to unsubscribe.</returns>
    public IDisposable Subscribe(Action<int> onNext)
    {
        ArgumentNullException.ThrowIfNull(onNext);
        return _throttledStream.Subscribe(onNext);
    }

    /// <summary>
    /// Subscribe to the throttled hover stream with error and completion handlers.
    /// </summary>
    public IDisposable Subscribe(Action<int> onNext, Action<Exception> onError, Action onCompleted)
    {
        ArgumentNullException.ThrowIfNull(onNext);
        ArgumentNullException.ThrowIfNull(onError);
        ArgumentNullException.ThrowIfNull(onCompleted);
        return _throttledStream.Subscribe(onNext, onError, onCompleted);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hoverSubject.OnCompleted();
        _disposables.Dispose();
    }
}
