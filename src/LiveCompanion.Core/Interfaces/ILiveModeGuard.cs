namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Garde de sécurité du mode Live.
/// Quand activé, empêche les actions destructives (suppression de sections,
/// modification de samples, changement de configuration audio).
/// </summary>
public interface ILiveModeGuard
{
    /// <summary>Indique si le mode Live est actif.</summary>
    bool IsLive { get; }

    /// <summary>Active le mode Live.</summary>
    void Engage();

    /// <summary>Désactive le mode Live.</summary>
    void Disengage();

    /// <summary>Déclenché quand le mode Live change d'état.</summary>
    event EventHandler<bool>? LiveModeChanged;
}
