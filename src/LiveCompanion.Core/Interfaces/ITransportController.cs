using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Contrat du contrôleur de transport. Pilote le Play/Pause/Stop et expose l'état courant.
/// </summary>
public interface ITransportController
{
    /// <summary>État courant du transport.</summary>
    TransportState State { get; }

    /// <summary>
    /// Déclenché à chaque changement d'état du transport.
    /// </summary>
    event EventHandler<TransportState> StateChanged;

    /// <summary>
    /// Lance la lecture depuis la position courante.
    /// Ne fait rien si le transport est déjà en lecture.
    /// </summary>
    Task PlayAsync();

    /// <summary>
    /// Met la lecture en pause. La position courante est conservée.
    /// Ne fait rien si le transport est déjà en pause ou arrêté.
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// Arrête la lecture et remet la position à zéro.
    /// </summary>
    Task StopAsync();
}
