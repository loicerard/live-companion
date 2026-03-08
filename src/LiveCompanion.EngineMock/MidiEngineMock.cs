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
    private readonly List<MidiSentRecord> _sentEvents = [];

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
    public IReadOnlyList<MidiSentRecord> SentEvents
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
    public Task SendEventAsync(MidiEvent midiEvent, IReadOnlyList<MidiProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(midiEvent);
        ArgumentNullException.ThrowIfNull(profiles);
        ThrowIfNotInitialized();

        foreach (var profileId in midiEvent.ProfileIds)
        {
            var profile = profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile?.DeviceOut is null)
            {
                _log.Warn(LogSource.EngineMock,
                    $"[MidiEngine] Profil {profileId} introuvable ou sans port — événement ignoré.");
                continue;
            }

            SendInternal(midiEvent.Type, profile.DeviceOut, profile.DefaultChannel,
                midiEvent.Data1, midiEvent.Data2);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SendDirectAsync(MidiEventType type, string deviceOut, int channel, int data1, int data2)
    {
        ThrowIfNotInitialized();
        SendInternal(type, deviceOut, channel, data1, data2);
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

    private void SendInternal(MidiEventType type, string deviceOut, int channel, int data1, int data2)
    {
        var record = new MidiSentRecord(type, deviceOut, channel, data1, data2);

        lock (_lock)
            _sentEvents.Add(record);

        _log.Debug(LogSource.EngineMock,
            $"[MidiEngine] Send — type={type}, " +
            $"device='{deviceOut}', ch={channel}, " +
            $"data1={data1}, data2={data2}");
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "MidiEngineMock n'est pas initialisé. Appelez InitializeAsync avant toute opération.");
    }
}

/// <summary>
/// Enregistrement d'un message MIDI envoyé (pour les tests et le débogage).
/// </summary>
public record MidiSentRecord(MidiEventType Type, string DeviceOut, int Channel, int Data1, int Data2);
