using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using System.Diagnostics;

namespace LiveCompanion.EngineMock;

/// <summary>
/// Simule le contrôleur de transport (Play/Pause/Stop) sans moteur audio réel.
/// Chaque transition d'état déclenche l'événement <see cref="StateChanged"/>.
/// Thread-safe : les mutations sont sérialisées via un <c>lock</c> ;
/// les événements sont levés hors du lock pour éviter les deadlocks.
/// </summary>
public sealed class TransportControllerMock : ITransportController
{
    private readonly object _lock = new();
    private TransportState _state = TransportState.Stopped;

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

        Debug.WriteLine($"[TransportControllerMock] State → {raised}");
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

        Debug.WriteLine($"[TransportControllerMock] State → {raised}");
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

        Debug.WriteLine($"[TransportControllerMock] State → {raised}");
        StateChanged?.Invoke(this, raised!.Value);
        return Task.CompletedTask;
    }
}
