using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Implémentation réelle du contrôleur de transport.
/// Stub : toutes les méthodes lèvent <see cref="NotImplementedException"/>.
/// </summary>
public sealed class TransportControllerReal : ITransportController
{
    /// <inheritdoc/>
    public TransportState State
        => throw new NotImplementedException("TODO: retourner l'état réel du transport.");

    /// <inheritdoc/>
    public event EventHandler<TransportState>? StateChanged;

    /// <inheritdoc/>
    public Task PlayAsync()
        => throw new NotImplementedException("TODO: démarrer la lecture réelle.");

    /// <inheritdoc/>
    public Task PauseAsync()
        => throw new NotImplementedException("TODO: mettre en pause la lecture réelle.");

    /// <inheritdoc/>
    public Task StopAsync()
        => throw new NotImplementedException("TODO: arrêter la lecture et remettre la position à zéro.");
}
