namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Fournit les niveaux audio peak par bus pour l'affichage de VU-mètres.
/// Séparé de <see cref="IAudioEngine"/> pour respecter le principe ISP.
/// </summary>
public interface IAudioMeterProvider
{
    /// <summary>
    /// Retourne les niveaux peak actuels par bus.
    /// Clé = nom du bus (ex: "Main", "Click"), Valeur = (Left, Right) entre 0.0 et 1.0.
    /// </summary>
    IReadOnlyDictionary<string, (float Left, float Right)> GetBusLevels();
}
