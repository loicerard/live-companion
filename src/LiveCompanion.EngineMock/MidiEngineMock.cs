using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineMock;

/// <summary>
/// Simule le moteur MIDI sans aucun device réel.
/// Tous les événements envoyés sont journalisés en mémoire.
/// Thread-safe : la liste d'événements est protégée par un <c>lock</c>.
/// </summary>
public sealed class MidiEngineMock : IMidiEngine
{
    private static readonly IReadOnlyList<string> _fakePorts =
        ["MockMIDI Port 1", "MockMIDI Port 2", "MockMIDI Port 3"];

    private readonly ILogService _log;
    private volatile bool _initialized;
    private MidiConfig? _config;

    private readonly object _lock = new();
    private readonly List<MidiEvent> _sentEvents = [];

    public MidiEngineMock(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    // ------------------------------------------------------------------ //
    // Propriété utilitaire
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Copie en lecture seule de tous les événements MIDI envoyés depuis l'initialisation.
    /// Thread-safe.
    /// </summary>
    public IReadOnlyList<MidiEvent> SentEvents
    {
        get { lock (_lock) return _sentEvents.ToList().AsReadOnly(); }
    }

    // ------------------------------------------------------------------ //
    // IMidiEngine
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public Task InitializeAsync(MidiConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _initialized = true;
        _log.Debug(LogSource.EngineMock, $"[MidiEngine] Initialized — ports=[{string.Join(", ", config.SelectedPorts)}]");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailablePorts() => _fakePorts;

    /// <inheritdoc/>
    public Task SendEventAsync(MidiEvent midiEvent)
    {
        ArgumentNullException.ThrowIfNull(midiEvent);
        ThrowIfNotInitialized();

        lock (_lock)
            _sentEvents.Add(midiEvent);

        _log.Debug(LogSource.EngineMock,
            $"[MidiEngine] Send — type={midiEvent.Type}, " +
            $"device='{midiEvent.DeviceOut}', ch={midiEvent.Channel}, " +
            $"data1={midiEvent.Data1}, data2={midiEvent.Data2}, " +
            $"pos={midiEvent.Position}");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShutdownAsync()
    {
        _initialized = false;
        _config = null;
        lock (_lock)
            _sentEvents.Clear();
        _log.Debug(LogSource.EngineMock, "[MidiEngine] Shutdown");
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "MidiEngineMock n'est pas initialisé. Appelez InitializeAsync avant toute opération.");
    }
}
