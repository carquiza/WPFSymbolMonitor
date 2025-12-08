namespace CryptoChart.App.Infrastructure;

/// <summary>
/// Manages CancellationTokenSource lifecycle for operations where starting
/// a new operation should cancel any previous pending operation.
/// </summary>
/// <remarks>
/// Use cases:
/// - Loading data for a newly selected item (cancel previous load)
/// - Switching between different views/modes
/// - Any scenario where only the latest request matters
/// </remarks>
public sealed class CancellationManager : IDisposable
{
    private CancellationTokenSource? _currentCts;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Gets a new CancellationToken, cancelling any previous token from this manager.
    /// The returned token will be cancelled when GetToken() is called again or
    /// when Cancel() or Dispose() is called.
    /// </summary>
    /// <returns>A fresh CancellationToken linked to this manager.</returns>
    public CancellationToken GetToken()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            // Cancel and dispose the previous token source
            _currentCts?.Cancel();
            _currentCts?.Dispose();

            // Create a new token source
            _currentCts = new CancellationTokenSource();
            return _currentCts.Token;
        }
    }

    /// <summary>
    /// Gets a new CancellationToken linked to both this manager and an external token.
    /// Useful when you need to respect an outer cancellation scope.
    /// </summary>
    /// <param name="externalToken">External token to link with.</param>
    /// <returns>A token that cancels when either source is cancelled.</returns>
    public CancellationToken GetLinkedToken(CancellationToken externalToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            // Cancel and dispose the previous token source
            _currentCts?.Cancel();
            _currentCts?.Dispose();

            // Create a new linked token source
            _currentCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            return _currentCts.Token;
        }
    }

    /// <summary>
    /// Cancels the current operation without getting a new token.
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _currentCts?.Cancel();
        }
    }

    /// <summary>
    /// Returns true if the current token has been cancelled.
    /// </summary>
    public bool IsCancellationRequested
    {
        get
        {
            lock (_lock)
            {
                return _currentCts?.IsCancellationRequested ?? false;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;
            _currentCts?.Cancel();
            _currentCts?.Dispose();
            _currentCts = null;
        }
    }
}

/// <summary>
/// Extension methods for working with cancellation in async operations.
/// </summary>
public static class CancellationExtensions
{
    /// <summary>
    /// Throws OperationCanceledException if the token is cancelled,
    /// useful for checking cancellation at logical points in long operations.
    /// </summary>
    public static void ThrowIfCancelled(this CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Returns true and performs no action if cancelled; useful for
    /// early-exit patterns without throwing exceptions.
    /// </summary>
    public static bool ShouldCancel(this CancellationToken token)
    {
        return token.IsCancellationRequested;
    }
}
