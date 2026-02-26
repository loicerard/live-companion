namespace LiveCompanion.UI;

/// <summary>
/// Détermine quel jeu d'implémentations moteur est enregistré dans le conteneur DI.
/// </summary>
public enum EngineMode
{
    /// <summary>Moteurs fictifs en mémoire (aucun matériel requis).</summary>
    Mock,

    /// <summary>Moteurs réels (ASIO, matériel MIDI).</summary>
    Real,
}
