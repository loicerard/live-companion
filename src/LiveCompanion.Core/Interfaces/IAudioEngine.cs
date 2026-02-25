using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Contrat du moteur audio. Gère l'initialisation du driver, la lecture des samples
/// et le routage vers les bus de sortie physiques.
/// </summary>
public interface IAudioEngine
{
    /// <summary>
    /// Initialise le moteur avec la configuration fournie (driver ASIO, buffer, mappings).
    /// </summary>
    /// <param name="config">Configuration audio à appliquer.</param>
    Task InitializeAsync(AudioConfig config);

    /// <summary>
    /// Retourne la liste des noms de drivers audio disponibles sur le système.
    /// </summary>
    IReadOnlyList<string> GetAvailableDrivers();

    /// <summary>
    /// Retourne la liste des tailles de buffer supportées par le driver actif.
    /// </summary>
    IReadOnlyList<int> GetSupportedBufferSizes();

    /// <summary>
    /// Déclenche la lecture d'un clip audio. La synchronisation suit le <see cref="SyncMode"/>
    /// du clip.
    /// </summary>
    /// <param name="clip">Clip à lire.</param>
    Task PlayClipAsync(AudioClip clip);

    /// <summary>
    /// Arrête immédiatement toutes les lectures en cours.
    /// </summary>
    Task StopAllAsync();

    /// <summary>Libère les ressources du moteur audio.</summary>
    Task ShutdownAsync();
}
