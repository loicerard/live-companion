using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineMock;

/// <summary>
/// Simule le moteur audio sans aucune I/O réelle.
/// Gère jusqu'à <see cref="MaxVoices"/> lectures simultanées fictives.
/// Thread-safe : le compteur de voix est modifié exclusivement via <see cref="Interlocked"/>.
/// </summary>
public sealed class AudioEngineMock : IAudioEngine
{
    /// <summary>Nombre maximum de voix simultanées simulées.</summary>
    public const int MaxVoices = 16;

    private static readonly IReadOnlyList<string> _fakeDrivers =
        ["MockASIO Driver", "MockWASAPI Driver"];

    private static readonly IReadOnlyList<int> _fakeBufferSizes =
        [64, 128, 256, 512, 1024];

    private readonly ILogService _log;
    private volatile bool _initialized;
    private int _activeVoices; // modified via Interlocked
    private AudioConfig? _config;

    public AudioEngineMock(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    // ------------------------------------------------------------------ //
    // Propriété utilitaire
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Nombre de voix simulées actuellement actives.
    /// Exposé pour permettre la composition avec <see cref="TimelineSchedulerMock"/>
    /// via un délégué <c>() => audioMock.ActiveVoices &gt; 0</c>.
    /// </summary>
    public int ActiveVoices => Volatile.Read(ref _activeVoices);

    // ------------------------------------------------------------------ //
    // IAudioEngine
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public Task InitializeAsync(AudioConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _initialized = true;
        _log.Debug(LogSource.EngineMock, $"[AudioEngine] Initialized — driver='{config.DriverName}', buffer={config.BufferSize}");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailableDrivers() => _fakeDrivers;

    /// <inheritdoc/>
    public IReadOnlyList<int> GetSupportedBufferSizes() => _fakeBufferSizes;

    /// <inheritdoc/>
    public Task PlayClipAsync(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ThrowIfNotInitialized();

        if (Volatile.Read(ref _activeVoices) >= MaxVoices)
        {
            _log.Warn(LogSource.EngineMock, $"[AudioEngine] Voice limit ({MaxVoices}) reached — clip '{clip.Name}' dropped.");
            return Task.CompletedTask;
        }

        Interlocked.Increment(ref _activeVoices);
        _log.Debug(LogSource.EngineMock, $"[AudioEngine] Playing '{clip.Name}' on bus '{clip.BusName}' " +
                        $"vol={clip.Volume:F2} — active voices={ActiveVoices}");

        // Simule une courte durée de lecture (200 ms) puis libère la voix.
        _ = Task.Run(async () =>
        {
            await Task.Delay(200).ConfigureAwait(false);
            Interlocked.Decrement(ref _activeVoices);
            _log.Debug(LogSource.EngineMock, $"[AudioEngine] Clip '{clip.Name}' ended — active voices={ActiveVoices}");
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAllAsync()
    {
        ThrowIfNotInitialized();
        Volatile.Write(ref _activeVoices, 0);
        _log.Debug(LogSource.EngineMock, "[AudioEngine] StopAll — active voices reset to 0");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShutdownAsync()
    {
        _initialized = false;
        Volatile.Write(ref _activeVoices, 0);
        _config = null;
        _log.Debug(LogSource.EngineMock, "[AudioEngine] Shutdown");
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "AudioEngineMock n'est pas initialisé. Appelez InitializeAsync avant toute opération.");
    }
}
