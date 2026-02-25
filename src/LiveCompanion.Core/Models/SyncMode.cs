namespace LiveCompanion.Core.Models;

/// <summary>
/// Mode de synchronisation d'un sample audio sur la grille rythmique.
/// </summary>
public enum SyncMode
{
    /// <summary>Lecture libre : démarre exactement à la position spécifiée.</summary>
    Free,

    /// <summary>Aligné sur la mesure : attend le prochain début de mesure.</summary>
    BarAligned,
}
