using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Implémentation réelle du moteur audio (ASIO).
/// Stub : toutes les méthodes lèvent <see cref="NotImplementedException"/>.
/// </summary>
public sealed class AudioEngineReal : IAudioEngine
{
    /// <inheritdoc/>
    public Task InitializeAsync(AudioConfig config)
        => throw new NotImplementedException("TODO: initialiser le driver ASIO réel.");

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailableDrivers()
        => throw new NotImplementedException("TODO: énumérer les drivers ASIO installés.");

    /// <inheritdoc/>
    public IReadOnlyList<int> GetSupportedBufferSizes()
        => throw new NotImplementedException("TODO: interroger le driver actif pour les tailles de buffer.");

    /// <inheritdoc/>
    public Task PlayClipAsync(AudioClip clip)
        => throw new NotImplementedException("TODO: déclencher la lecture du clip via le moteur audio.");

    /// <inheritdoc/>
    public Task StopAllAsync()
        => throw new NotImplementedException("TODO: arrêter toutes les lectures en cours.");

    /// <inheritdoc/>
    public Task ShutdownAsync()
        => throw new NotImplementedException("TODO: libérer les ressources du driver audio.");
}
