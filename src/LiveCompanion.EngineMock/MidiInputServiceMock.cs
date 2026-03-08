using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineMock;

/// <summary>
/// Implémentation mock du service MIDI IN.
/// Utilise <see cref="SimulateInput"/> pour injecter des messages MIDI dans les tests.
/// </summary>
public sealed class MidiInputServiceMock : IMidiInputService
{
    private static readonly IReadOnlyList<string> _fakePorts =
        ["MockMIDI IN 1", "MockMIDI IN 2"];

    private readonly ILogService _log;
    private IReadOnlyList<MidiTransportMap> _mappings = [];
    private volatile bool _learning;
    private volatile bool _running;

    public MidiInputServiceMock(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    // ------------------------------------------------------------------ //
    // IMidiInputService
    // ------------------------------------------------------------------ //

    public IReadOnlyList<string> GetAvailableInputPorts() => _fakePorts;

    public void Start(string portName, IReadOnlyList<MidiTransportMap> mappings)
    {
        _mappings = mappings ?? [];
        _running = true;
        _log.Debug(LogSource.EngineMock,
            $"[MidiInput] Started on '{portName}' with {_mappings.Count} mapping(s)");
    }

    public void Stop()
    {
        _running = false;
        _learning = false;
        _log.Debug(LogSource.EngineMock, "[MidiInput] Stopped");
    }

    public event EventHandler<TransportAction>? TransportActionReceived;

    public void StartLearn()
    {
        _learning = true;
        _log.Debug(LogSource.EngineMock, "[MidiInput] Learn started");
    }

    public void StopLearn()
    {
        _learning = false;
        _log.Debug(LogSource.EngineMock, "[MidiInput] Learn stopped");
    }

    public event EventHandler<MidiLearnResult>? MidiLearnReceived;

    // ------------------------------------------------------------------ //
    // API de test
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Simule la réception d'un message MIDI entrant.
    /// En mode Learn, fire <see cref="MidiLearnReceived"/> puis désactive le Learn.
    /// Sinon, cherche un mapping correspondant et fire <see cref="TransportActionReceived"/>.
    /// </summary>
    public void SimulateInput(MidiEventType type, int channel, int data1, int data2 = 0)
    {
        if (_learning)
        {
            _learning = false;
            MidiLearnReceived?.Invoke(this, new MidiLearnResult(type, channel, data1));
            return;
        }

        if (!_running) return;

        foreach (var map in _mappings)
        {
            if (!map.IsAssigned) continue;
            if (map.EventType != type) continue;
            if (map.Data1 != data1) continue;
            if (map.Channel.HasValue && map.Channel != channel) continue;

            _log.Debug(LogSource.EngineMock,
                $"[MidiInput] Action '{map.Action}' triggered by {type} #{data1} ch.{channel}");
            TransportActionReceived?.Invoke(this, map.Action);
            return;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
