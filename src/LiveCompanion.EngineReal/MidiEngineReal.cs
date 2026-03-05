using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using NAudio.Midi;

using MidiEvent = LiveCompanion.Core.Models.MidiEvent;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Implémentation réelle du moteur MIDI via NAudio.
/// Gère l'énumération des ports MIDI OUT, l'ouverture/fermeture des devices,
/// et l'envoi d'événements MIDI (ProgramChange, ControlChange, NoteOn, NoteOff).
/// <para>
/// Thread-safe : l'accès aux ports ouverts est protégé par un <c>lock</c>.
/// Supporte jusqu'à 6 devices simultanés (conformément à <see cref="MidiConfig.SelectedPorts"/>).
/// </para>
/// </summary>
public sealed class MidiEngineReal : IMidiEngine, IDisposable
{
    private readonly ILogService _log;
    private readonly object _lock = new();

    /// <summary>
    /// Ports MIDI ouverts, indexés par nom de device.
    /// </summary>
    private readonly Dictionary<string, MidiOut> _openPorts = new(StringComparer.OrdinalIgnoreCase);

    private volatile bool _initialized;

    public MidiEngineReal(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    // ------------------------------------------------------------------ //
    // IMidiEngine
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailablePorts()
    {
        var ports = new List<string>();

        try
        {
            int count = MidiOut.NumberOfDevices;

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var info = MidiOut.DeviceInfo(i);
                    ports.Add(info.ProductName);
                }
                catch (Exception ex)
                {
                    _log.Warn(LogSource.EngineReal,
                        $"[MidiEngine] Impossible de lire le device MIDI #{i} — {ex.Message}");
                }
            }
        }
        catch (DllNotFoundException)
        {
            _log.Warn(LogSource.EngineReal,
                "[MidiEngine] API MIDI non disponible sur cette plateforme (winmm.dll introuvable).");
        }

        _log.Debug(LogSource.EngineReal,
            $"[MidiEngine] {ports.Count} port(s) MIDI OUT détecté(s) : [{string.Join(", ", ports)}]");

        return ports.AsReadOnly();
    }

    /// <inheritdoc/>
    public Task InitializeAsync(MidiConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_lock)
        {
            // Fermer les ports déjà ouverts (réinitialisation)
            CloseAllPorts();

            foreach (var portName in config.SelectedPorts)
            {
                int deviceIndex = FindDeviceIndex(portName);
                if (deviceIndex < 0)
                {
                    _log.Warn(LogSource.EngineReal,
                        $"[MidiEngine] Port '{portName}' introuvable — ignoré.");
                    continue;
                }

                try
                {
                    var midiOut = new MidiOut(deviceIndex);
                    _openPorts[portName] = midiOut;
                    _log.Info(LogSource.EngineReal,
                        $"[MidiEngine] Port ouvert : '{portName}' (device #{deviceIndex})");
                }
                catch (Exception ex)
                {
                    _log.Error(LogSource.EngineReal,
                        $"[MidiEngine] Impossible d'ouvrir '{portName}' — {ex.Message}");
                }
            }

            _initialized = true;
        }

        _log.Debug(LogSource.EngineReal,
            $"[MidiEngine] Initialized — {_openPorts.Count}/{config.SelectedPorts.Count} port(s) ouvert(s)");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SendEventAsync(MidiEvent midiEvent)
    {
        ArgumentNullException.ThrowIfNull(midiEvent);
        ThrowIfNotInitialized();

        lock (_lock)
        {
            if (!_openPorts.TryGetValue(midiEvent.DeviceOut, out var midiOut))
            {
                _log.Warn(LogSource.EngineReal,
                    $"[MidiEngine] Device '{midiEvent.DeviceOut}' non ouvert — événement ignoré.");
                return Task.CompletedTask;
            }

            int message = BuildMidiMessage(midiEvent);
            midiOut.Send(message);
        }

        _log.Debug(LogSource.EngineReal,
            $"[MidiEngine] Send — type={midiEvent.Type}, " +
            $"device='{midiEvent.DeviceOut}', ch={midiEvent.Channel}, " +
            $"data1={midiEvent.Data1}, data2={midiEvent.Data2}");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ShutdownAsync()
    {
        lock (_lock)
        {
            CloseAllPorts();
            _initialized = false;
        }

        _log.Debug(LogSource.EngineReal, "[MidiEngine] Shutdown");
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // IDisposable
    // ------------------------------------------------------------------ //

    public void Dispose()
    {
        lock (_lock)
            CloseAllPorts();
    }

    // ------------------------------------------------------------------ //
    // MIDI message building
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Construit le message MIDI 32 bits (short message) attendu par <see cref="MidiOut.Send"/>.
    /// Format : status | (data1 &lt;&lt; 8) | (data2 &lt;&lt; 16).
    /// </summary>
    internal static int BuildMidiMessage(MidiEvent evt)
    {
        int channel = Math.Clamp(evt.Channel, 1, 16) - 1; // 0-based
        int data1 = Math.Clamp(evt.Data1, 0, 127);
        int data2 = Math.Clamp(evt.Data2, 0, 127);

        int status = evt.Type switch
        {
            MidiEventType.NoteOn => 0x90 | channel,
            MidiEventType.NoteOff => 0x80 | channel,
            MidiEventType.ControlChange => 0xB0 | channel,
            MidiEventType.ProgramChange => 0xC0 | channel,
            _ => throw new ArgumentOutOfRangeException(nameof(evt),
                $"Type MIDI non supporté : {evt.Type}"),
        };

        // ProgramChange n'a qu'un seul octet de données
        if (evt.Type == MidiEventType.ProgramChange)
            return status | (data1 << 8);

        return status | (data1 << 8) | (data2 << 16);
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Recherche l'index du device MIDI OUT correspondant au nom donné.
    /// Retourne -1 si non trouvé.
    /// </summary>
    private static int FindDeviceIndex(string portName)
    {
        try
        {
            int count = MidiOut.NumberOfDevices;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var info = MidiOut.DeviceInfo(i);
                    if (string.Equals(info.ProductName, portName, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
                catch
                {
                    // Device inaccessible — on continue
                }
            }
        }
        catch (DllNotFoundException)
        {
            // API MIDI non disponible (non-Windows)
        }
        return -1;
    }

    private void CloseAllPorts()
    {
        foreach (var kvp in _openPorts)
        {
            try
            {
                kvp.Value.Dispose();
                _log.Debug(LogSource.EngineReal, $"[MidiEngine] Port fermé : '{kvp.Key}'");
            }
            catch (Exception ex)
            {
                _log.Warn(LogSource.EngineReal,
                    $"[MidiEngine] Erreur fermeture '{kvp.Key}' — {ex.Message}");
            }
        }
        _openPorts.Clear();
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "MidiEngineReal n'est pas initialisé. Appelez InitializeAsync avant toute opération.");
    }
}
