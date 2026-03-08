using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineMock;

/// <summary>
/// Simule le moteur audio sans aucune I/O réelle.
/// Gère jusqu'à <see cref="MaxVoices"/> lectures simultanées fictives.
/// Thread-safe : le compteur de voix est modifié exclusivement via <see cref="Interlocked"/>.
/// </summary>
public sealed class AudioEngineMock : IAudioEngine, IAudioMeterProvider, IAudioMixerProvider
{
    /// <summary>Nombre maximum de voix simultanées simulées.</summary>
    public const int MaxVoices = 16;

    private static readonly IReadOnlyList<string> _fakeDrivers =
        ["MockASIO Driver", "MockWASAPI Driver"];

    private static readonly IReadOnlyList<int> _fakeBufferSizes =
        [64, 128, 256, 512, 1024];

    private static readonly string[] DefaultBusNames = ["Main", "Click"];

    private readonly ILogService _log;
    private readonly Random _random = new();
    private readonly Dictionary<string, float> _busVolumes = new();
    private volatile bool _initialized;
    private int _activeVoices; // modified via Interlocked
    private CancellationTokenSource _voiceCts = new(); // annulé par StopAll/Shutdown
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
    public IReadOnlyList<string> GetAvailableOutputPairs() =>
        ["Output 1-2", "Output 3-4", "Output 5-6", "Output 7-8"];

    /// <inheritdoc/>
    public Task PreloadAsync(IEnumerable<string> filePaths) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task PlayClipAsync(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ThrowIfNotInitialized();

        foreach (var send in clip.Sends)
        {
            if (Volatile.Read(ref _activeVoices) >= MaxVoices)
            {
                _log.Warn(LogSource.EngineMock, $"[AudioEngine] Voice limit ({MaxVoices}) reached — clip '{clip.Name}' send '{send.BusName}' dropped.");
                continue;
            }

            Interlocked.Increment(ref _activeVoices);
            _log.Debug(LogSource.EngineMock, $"[AudioEngine] Playing '{clip.Name}' on bus '{send.BusName}' " +
                            $"vol={send.Volume:F2} — active voices={ActiveVoices}");

            // Simule une courte durée de lecture (200 ms) puis libère la voix.
            // Le CancellationToken empêche le Decrement si StopAll a été appelé entretemps.
            var token = _voiceCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(200, token).ConfigureAwait(false);
                    Interlocked.Decrement(ref _activeVoices);
                    _log.Debug(LogSource.EngineMock, $"[AudioEngine] Clip '{clip.Name}' ended — active voices={ActiveVoices}");
                }
                catch (OperationCanceledException)
                {
                    // StopAll/Shutdown a été appelé — le compteur a déjà été remis à 0
                }
            });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAllAsync()
    {
        ThrowIfNotInitialized();
        // Annuler les Task.Delay en cours pour empêcher les Decrement orphelins
        _voiceCts.Cancel();
        _voiceCts.Dispose();
        _voiceCts = new CancellationTokenSource();
        Volatile.Write(ref _activeVoices, 0);
        _log.Debug(LogSource.EngineMock, "[AudioEngine] StopAll — active voices reset to 0");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShutdownAsync()
    {
        _initialized = false;
        _voiceCts.Cancel();
        _voiceCts.Dispose();
        _voiceCts = new CancellationTokenSource();
        Volatile.Write(ref _activeVoices, 0);
        _config = null;
        _log.Debug(LogSource.EngineMock, "[AudioEngine] Shutdown");
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // IAudioMeterProvider
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, (float Left, float Right)> GetBusLevels()
    {
        int voices = ActiveVoices;
        var busNames = _config?.BusMappings.Keys ?? (IEnumerable<string>)DefaultBusNames;

        var levels = new Dictionary<string, (float Left, float Right)>();
        foreach (var bus in busNames)
        {
            if (voices == 0)
            {
                levels[bus] = (0f, 0f);
            }
            else
            {
                // Simuler un niveau proportionnel aux voix actives avec un léger jitter
                float baseLevel = MathF.Min(0.2f + voices * 0.05f, 0.8f);
                float jitter = (float)(_random.NextDouble() * 0.1);
                // Appliquer le volume master du bus (post-fader)
                float vol = _busVolumes.GetValueOrDefault(bus, 1.0f);
                float level = (baseLevel + jitter) * vol;
                levels[bus] = (level, level);
            }
        }

        return levels;
    }

    // ------------------------------------------------------------------ //
    // IAudioMixerProvider
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public IReadOnlyList<string> GetBusNames()
        => (_config?.BusMappings.Keys ?? (IEnumerable<string>)DefaultBusNames).ToList();

    /// <inheritdoc/>
    public float GetBusVolume(string busName)
        => _busVolumes.GetValueOrDefault(busName, 1.0f);

    /// <inheritdoc/>
    public void SetBusVolume(string busName, float volume)
        => _busVolumes[busName] = Math.Clamp(volume, 0f, 1f);

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
