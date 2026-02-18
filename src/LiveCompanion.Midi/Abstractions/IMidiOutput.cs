namespace LiveCompanion.Midi.Abstractions;

/// <summary>
/// Abstraction over a MIDI output port. Implementations wrap NAudio's MidiOut.
/// Allows faking in tests without real MIDI hardware.
/// </summary>
public interface IMidiOutput : IDisposable
{
    /// <summary>Name of the underlying MIDI output port.</summary>
    string PortName { get; }

    /// <summary>
    /// Sends a short MIDI message (1–3 bytes).
    /// The message is packed as: status | (data1 &lt;&lt; 8) | (data2 &lt;&lt; 16),
    /// matching NAudio's MidiOut.Send(int) convention.
    /// </summary>
    void Send(int packedMessage);
}
