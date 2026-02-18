using LiveCompanion.Core.Models;
using System.Text.Json.Serialization;

namespace LiveCompanion.Midi;

/// <summary>
/// Serializable MIDI configuration. Maps hardware devices to MIDI ports,
/// defines the MIDI input mapping, and controls which devices receive MIDI Clock.
/// </summary>
public sealed class MidiConfiguration
{
    /// <summary>
    /// MIDI output routing per device target.
    /// Maps each <see cref="DeviceTarget"/> to its output port and MIDI channel.
    /// </summary>
    public Dictionary<DeviceTarget, DeviceOutputConfig> OutputDevices { get; set; } = new()
    {
        [DeviceTarget.Quad1] = new DeviceOutputConfig { PortName = "Quad Cortex 1", Channel = 0 },
        [DeviceTarget.Quad2] = new DeviceOutputConfig { PortName = "Quad Cortex 2", Channel = 0 },
        [DeviceTarget.SSPD]  = new DeviceOutputConfig { PortName = "Roland SSPD",   Channel = 0 },
    };

    /// <summary>
    /// Name of the MIDI input port to listen on (e.g. from the Roland SSPD footswitch).
    /// Null or empty to disable MIDI input.
    /// </summary>
    public string? MidiInputPortName { get; set; }

    /// <summary>
    /// Table mapping incoming MIDI messages to player actions.
    /// Each entry describes a specific MIDI message that triggers a remote control action.
    /// </summary>
    public List<MidiInputMapping> InputMappings { get; set; } = [];

    /// <summary>
    /// Devices that should receive MIDI Clock (0xF8), Start (0xFA), Stop (0xFC),
    /// and Continue (0xFB) messages. Typically the Quad Cortex units for BPM sync.
    /// </summary>
    public List<DeviceTarget> ClockTargets { get; set; } =
    [
        DeviceTarget.Quad1,
        DeviceTarget.Quad2,
    ];

    /// <summary>
    /// Delay between automatic reconnect attempts when a MIDI port becomes unavailable.
    /// </summary>
    public int ReconnectDelayMs { get; set; } = 5000;
}

/// <summary>
/// MIDI output port configuration for a single device target.
/// The channel stored here is the default; each MidiPreset can override it.
/// </summary>
public sealed class DeviceOutputConfig
{
    /// <summary>System MIDI port name as reported by the OS (e.g. "Quad Cortex").</summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>
    /// Default MIDI channel (0-based, i.e. 0 = MIDI channel 1, 15 = MIDI channel 16).
    /// MidiPreset.Channel overrides this per-event.
    /// </summary>
    public int Channel { get; set; }
}

/// <summary>
/// Player action that can be triggered via MIDI input.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MidiAction
{
    Stop,
    Pause,
    NextSong,
    PreviousSong,
    TriggerCue,
}

/// <summary>
/// Maps a specific incoming MIDI message to a <see cref="MidiAction"/>.
/// All fields are ANDed: a message matches only when all specified fields match.
/// </summary>
public sealed class MidiInputMapping
{
    /// <summary>
    /// MIDI status byte (lower nibble stripped for channel messages).
    /// Examples: 0xB0 = Control Change, 0x90 = Note On, 0xC0 = Program Change.
    /// </summary>
    public byte StatusType { get; set; }

    /// <summary>
    /// MIDI channel (0-based). -1 matches any channel.
    /// Only relevant for channel messages (status &lt; 0xF0).
    /// </summary>
    public int Channel { get; set; } = -1;

    /// <summary>
    /// First data byte to match (controller number, note number, etc.).
    /// -1 matches any value.
    /// </summary>
    public int Data1 { get; set; } = -1;

    /// <summary>
    /// Second data byte to match (CC value, velocity, etc.).
    /// -1 matches any value.
    /// </summary>
    public int Data2 { get; set; } = -1;

    /// <summary>Action to invoke when this mapping matches.</summary>
    public MidiAction Action { get; set; }

    /// <summary>
    /// Returns true if the given raw packed message matches this mapping.
    /// </summary>
    public bool Matches(byte status, byte data1, byte data2)
    {
        // For channel messages, strip the channel nibble from the status byte
        bool isChannelMessage = status < 0xF0;
        byte statusType = isChannelMessage ? (byte)(status & 0xF0) : status;
        int channel = isChannelMessage ? status & 0x0F : -1;

        if (statusType != StatusType) return false;
        if (Channel >= 0 && isChannelMessage && channel != Channel) return false;
        if (Data1 >= 0 && data1 != Data1) return false;
        if (Data2 >= 0 && data2 != Data2) return false;
        return true;
    }
}
