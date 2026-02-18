namespace LiveCompanion.Midi.Abstractions;

/// <summary>
/// Arguments for a received MIDI input message.
/// </summary>
public sealed class MidiMessageReceivedEventArgs : EventArgs
{
    public MidiMessageReceivedEventArgs(int packedMessage, long timestamp)
    {
        PackedMessage = packedMessage;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Raw packed message: status | (data1 &lt;&lt; 8) | (data2 &lt;&lt; 16).
    /// Matching NAudio's MidiInMessageEventArgs.RawMessage convention.
    /// </summary>
    public int PackedMessage { get; }

    /// <summary>MIDI driver timestamp in milliseconds.</summary>
    public long Timestamp { get; }

    /// <summary>MIDI status byte (channel stripped for channel messages).</summary>
    public byte Status => (byte)(PackedMessage & 0xFF);

    /// <summary>First data byte (controller number, note, etc.).</summary>
    public byte Data1 => (byte)((PackedMessage >> 8) & 0xFF);

    /// <summary>Second data byte (value, velocity, etc.).</summary>
    public byte Data2 => (byte)((PackedMessage >> 16) & 0xFF);
}

/// <summary>
/// Abstraction over a MIDI input port. Implementations wrap NAudio's MidiIn.
/// Allows faking in tests without real MIDI hardware.
/// </summary>
public interface IMidiInput : IDisposable
{
    /// <summary>Name of the underlying MIDI input port.</summary>
    string PortName { get; }

    /// <summary>Fired on the NAudio MIDI callback thread when a message is received.</summary>
    event EventHandler<MidiMessageReceivedEventArgs> MessageReceived;

    /// <summary>Starts listening for MIDI messages.</summary>
    void Start();

    /// <summary>Stops listening.</summary>
    void Stop();
}
