using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiveCompanion.Midi;

/// <summary>
/// Routes MIDI Program Change and Control Change messages to the correct hardware
/// device and MIDI channel, based on the <see cref="MidiPreset"/> payload emitted
/// by <see cref="SetlistPlayer.MidiPresetChanged"/>.
///
/// Routing table (from <see cref="MidiConfiguration.OutputDevices"/>):
///   DeviceTarget.Quad1 → MIDI output port "Quad Cortex 1"
///   DeviceTarget.Quad2 → MIDI output port "Quad Cortex 2"
///   DeviceTarget.SSPD  → MIDI output port "Roland SSPD"
///
/// Each preset carries its own MIDI channel (0-based).
/// PC message: 0xC0 | channel, program, 0
/// CC message: 0xB0 | channel, controller, value
/// </summary>
public sealed class MidiRouter : IDisposable
{
    private readonly MidiService _midiService;
    private readonly MidiConfiguration _config;
    private readonly ILogger<MidiRouter> _logger;
    private SetlistPlayer? _player;
    private bool _disposed;

    public MidiRouter(MidiService midiService, MidiConfiguration config,
        ILogger<MidiRouter>? logger = null)
    {
        _midiService = midiService ?? throw new ArgumentNullException(nameof(midiService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? NullLogger<MidiRouter>.Instance;
    }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>
    /// Subscribes to <see cref="SetlistPlayer.MidiPresetChanged"/> so that
    /// Program Changes and CCs are forwarded to hardware automatically.
    /// </summary>
    public void Attach(SetlistPlayer player)
    {
        Detach();
        _player = player;
        _player.MidiPresetChanged += OnMidiPresetChanged;
    }

    /// <summary>Unsubscribes from the previously attached <see cref="SetlistPlayer"/>.</summary>
    public void Detach()
    {
        if (_player is null) return;
        _player.MidiPresetChanged -= OnMidiPresetChanged;
        _player = null;
    }

    /// <summary>
    /// Sends the Program Change and all CC messages in the given preset
    /// to the appropriate device. Called automatically via the SetlistPlayer event,
    /// and also available for direct invocation (e.g. emergency preset recall).
    /// </summary>
    public void SendPreset(MidiPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        if (!_config.OutputDevices.TryGetValue(preset.Device, out var deviceConfig))
        {
            _logger.LogWarning(
                "No output port configured for device '{Device}'. Preset ignored.",
                preset.Device);
            return;
        }

        string portName = deviceConfig.PortName;
        // Channel is taken from the preset (0-based). The DeviceOutputConfig.Channel
        // serves as a default when the preset sets Channel to -1 (future extension).
        int channel = preset.Channel >= 0 ? preset.Channel : deviceConfig.Channel;

        // Program Change: status 0xCn, data1 = program number, data2 = 0
        int pcMessage = BuildProgramChange(channel, preset.ProgramChange);
        _midiService.Send(portName, pcMessage);
        _logger.LogDebug(
            "PC → {Device} ({Port}) ch{Channel} prog={Program}",
            preset.Device, portName, channel + 1, preset.ProgramChange);

        // Control Changes: status 0xBn, data1 = controller, data2 = value
        foreach (var cc in preset.ControlChanges)
        {
            int ccMessage = BuildControlChange(channel, cc.Controller, cc.Value);
            _midiService.Send(portName, ccMessage);
            _logger.LogDebug(
                "CC → {Device} ({Port}) ch{Channel} cc{Controller}={Value}",
                preset.Device, portName, channel + 1, cc.Controller, cc.Value);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
    }

    // ── MIDI message builders ─────────────────────────────────────

    /// <summary>
    /// Builds a packed Program Change message.
    /// Status: 0xC0 | channel (0-based)
    /// Data1: program (0-based)
    /// Data2: 0
    /// </summary>
    internal static int BuildProgramChange(int channel, int program)
    {
        byte status = (byte)(0xC0 | (channel & 0x0F));
        byte data1 = (byte)(program & 0x7F);
        return status | (data1 << 8);
    }

    /// <summary>
    /// Builds a packed Control Change message.
    /// Status: 0xB0 | channel (0-based)
    /// Data1: controller number
    /// Data2: value (0-127)
    /// </summary>
    internal static int BuildControlChange(int channel, int controller, int value)
    {
        byte status = (byte)(0xB0 | (channel & 0x0F));
        byte data1 = (byte)(controller & 0x7F);
        byte data2 = (byte)(value & 0x7F);
        return status | (data1 << 8) | (data2 << 16);
    }

    // ── Event handlers ────────────────────────────────────────────

    private void OnMidiPresetChanged(MidiPreset preset) => SendPreset(preset);
}
