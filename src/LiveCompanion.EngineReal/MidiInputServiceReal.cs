using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using NAudio.Midi;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Implémentation réelle du service MIDI IN via NAudio.
/// Écoute un port MIDI IN et déclenche les actions de transport correspondantes.
/// Supporte le mode MIDI Learn pour assigner les mappings interactivement.
/// Thread-safe : l'accès au port et aux mappings est protégé par un lock.
/// </summary>
public sealed class MidiInputServiceReal : IMidiInputService
{
    private readonly ILogService _log;
    private readonly object _lock = new();

    private MidiIn? _midiIn;
    private IReadOnlyList<MidiTransportMap> _mappings = [];
    private volatile bool _learning;

    public MidiInputServiceReal(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    // ------------------------------------------------------------------ //
    // IMidiInputService
    // ------------------------------------------------------------------ //

    public IReadOnlyList<string> GetAvailableInputPorts()
    {
        var ports = new List<string>();

        try
        {
            int count = MidiIn.NumberOfDevices;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    ports.Add(MidiIn.DeviceInfo(i).ProductName);
                }
                catch (Exception ex)
                {
                    _log.Warn(LogSource.EngineReal,
                        $"[MidiInput] Impossible de lire le device IN #{i} — {ex.Message}");
                }
            }
        }
        catch (DllNotFoundException)
        {
            _log.Warn(LogSource.EngineReal,
                "[MidiInput] API MIDI non disponible (winmm.dll introuvable).");
        }

        _log.Debug(LogSource.EngineReal,
            $"[MidiInput] {ports.Count} port(s) MIDI IN détecté(s) : [{string.Join(", ", ports)}]");

        return ports.AsReadOnly();
    }

    public void Start(string portName, IReadOnlyList<MidiTransportMap> mappings)
    {
        ArgumentNullException.ThrowIfNull(portName);
        ArgumentNullException.ThrowIfNull(mappings);

        lock (_lock)
        {
            ClosePort();

            _mappings = mappings;

            int deviceIndex = FindDeviceIndex(portName);
            if (deviceIndex < 0)
            {
                _log.Warn(LogSource.EngineReal,
                    $"[MidiInput] Port IN '{portName}' introuvable.");
                return;
            }

            try
            {
                _midiIn = new MidiIn(deviceIndex);
                _midiIn.MessageReceived += OnMessageReceived;
                _midiIn.ErrorReceived += OnErrorReceived;
                _midiIn.Start();

                _log.Info(LogSource.EngineReal,
                    $"[MidiInput] Démarré sur '{portName}' (device #{deviceIndex}), " +
                    $"{mappings.Count} mapping(s)");
            }
            catch (Exception ex)
            {
                _log.Error(LogSource.EngineReal,
                    $"[MidiInput] Impossible d'ouvrir '{portName}' — {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            ClosePort();
            _learning = false;
        }
        _log.Debug(LogSource.EngineReal, "[MidiInput] Arrêté");
    }

    public event EventHandler<TransportAction>? TransportActionReceived;

    public void StartLearn()
    {
        _learning = true;
        _log.Debug(LogSource.EngineReal, "[MidiInput] Mode Learn activé");
    }

    public void StopLearn()
    {
        _learning = false;
        _log.Debug(LogSource.EngineReal, "[MidiInput] Mode Learn désactivé");
    }

    public event EventHandler<MidiLearnResult>? MidiLearnReceived;

    // ------------------------------------------------------------------ //
    // IDisposable
    // ------------------------------------------------------------------ //

    public void Dispose()
    {
        lock (_lock)
            ClosePort();
    }

    // ------------------------------------------------------------------ //
    // Gestion des messages entrants
    // ------------------------------------------------------------------ //

    private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        // Décoder le message NAudio
        if (!TryDecodeMessage(e.MidiEvent, out var type, out int channel, out int data1, out int data2))
            return;

        _log.Debug(LogSource.EngineReal,
            $"[MidiInput] Reçu {type} #{data1} val={data2} ch.{channel}");

        // Mode Learn : capture le premier message et désactive
        if (_learning)
        {
            _learning = false;
            MidiLearnReceived?.Invoke(this, new MidiLearnResult(type, channel, data1));
            return;
        }

        // Mode normal : chercher un mapping correspondant
        foreach (var map in _mappings)
        {
            if (!map.IsAssigned) continue;
            if (map.EventType != type) continue;
            if (map.Data1 != data1) continue;
            if (map.Channel.HasValue && map.Channel != channel) continue;

            _log.Debug(LogSource.EngineReal,
                $"[MidiInput] Action '{map.Action}' déclenchée");
            TransportActionReceived?.Invoke(this, map.Action);
            return;
        }
    }

    private void OnErrorReceived(object? sender, MidiInMessageEventArgs e)
    {
        _log.Warn(LogSource.EngineReal,
            $"[MidiInput] Erreur MIDI reçue — RawMessage=0x{e.RawMessage:X8}");
    }

    /// <summary>
    /// Décode un événement NAudio en type/channel/data1/data2 compatibles avec nos modèles.
    /// Retourne false si le type n'est pas supporté (ProgramChange, SysEx, etc.).
    /// </summary>
    private static bool TryDecodeMessage(
        NAudio.Midi.MidiEvent naudioEvent,
        out MidiEventType type,
        out int channel,
        out int data1,
        out int data2)
    {
        type = default;
        channel = 1;
        data1 = 0;
        data2 = 0;

        switch (naudioEvent)
        {
            case NoteOnEvent noteOn when noteOn.Velocity > 0:
                type = MidiEventType.NoteOn;
                channel = noteOn.Channel;
                data1 = noteOn.NoteNumber;
                data2 = noteOn.Velocity;
                return true;

            case NoteEvent noteOff when noteOff.CommandCode == MidiCommandCode.NoteOff:
                type = MidiEventType.NoteOff;
                channel = noteOff.Channel;
                data1 = noteOff.NoteNumber;
                data2 = noteOff.Velocity;
                return true;

            case ControlChangeEvent cc:
                type = MidiEventType.ControlChange;
                channel = cc.Channel;
                data1 = (int)cc.Controller;
                data2 = cc.ControllerValue;
                return true;

            default:
                return false;
        }
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private void ClosePort()
    {
        if (_midiIn is null) return;

        try
        {
            _midiIn.Stop();
            _midiIn.MessageReceived -= OnMessageReceived;
            _midiIn.ErrorReceived -= OnErrorReceived;
            _midiIn.Dispose();
            _log.Debug(LogSource.EngineReal, "[MidiInput] Port fermé");
        }
        catch (Exception ex)
        {
            _log.Warn(LogSource.EngineReal,
                $"[MidiInput] Erreur fermeture port — {ex.Message}");
        }
        finally
        {
            _midiIn = null;
        }
    }

    private static int FindDeviceIndex(string portName)
    {
        try
        {
            int count = MidiIn.NumberOfDevices;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    if (string.Equals(MidiIn.DeviceInfo(i).ProductName, portName,
                            StringComparison.OrdinalIgnoreCase))
                        return i;
                }
                catch { }
            }
        }
        catch (DllNotFoundException) { }
        return -1;
    }
}
