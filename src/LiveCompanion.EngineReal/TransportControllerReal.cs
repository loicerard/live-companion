using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Real transport controller. State machine with thread-safe transitions.
/// Same pattern as <c>TransportControllerMock</c> — events are raised outside the lock.
/// ASIO start/stop integration will be added in Phase 5.
/// </summary>
public sealed class TransportControllerReal : ITransportController
{
    private readonly ILogService _log;
    private readonly object _lock = new();
    private TransportState _state = TransportState.Stopped;

    public TransportControllerReal(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    // ------------------------------------------------------------------ //
    // ITransportController
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public TransportState State
    {
        get { lock (_lock) return _state; }
    }

    /// <inheritdoc/>
    public event EventHandler<TransportState>? StateChanged;

    /// <inheritdoc/>
    public Task PlayAsync()
    {
        TransportState? raised = null;

        lock (_lock)
        {
            if (_state == TransportState.Playing)
                return Task.CompletedTask;

            _state = TransportState.Playing;
            raised = _state;
        }

        _log.Debug(LogSource.EngineReal, $"[Transport] State → {raised}");
        StateChanged?.Invoke(this, raised!.Value);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task PauseAsync()
    {
        TransportState? raised = null;

        lock (_lock)
        {
            if (_state is TransportState.Paused or TransportState.Stopped)
                return Task.CompletedTask;

            _state = TransportState.Paused;
            raised = _state;
        }

        _log.Debug(LogSource.EngineReal, $"[Transport] State → {raised}");
        StateChanged?.Invoke(this, raised!.Value);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        TransportState? raised = null;

        lock (_lock)
        {
            if (_state == TransportState.Stopped)
                return Task.CompletedTask;

            _state = TransportState.Stopped;
            raised = _state;
        }

        _log.Debug(LogSource.EngineReal, $"[Transport] State → {raised}");
        StateChanged?.Invoke(this, raised!.Value);
        return Task.CompletedTask;
    }
}
