namespace LiveCompanion.Midi.Abstractions;

/// <summary>
/// Factory that enumerates and opens MIDI ports.
/// The NAudio implementation uses MidiOut/MidiIn under the hood.
/// A fake implementation is used in tests.
/// </summary>
public interface IMidiPortFactory
{
    /// <summary>Returns the names of all available MIDI output ports.</summary>
    string[] GetOutputPortNames();

    /// <summary>Returns the names of all available MIDI input ports.</summary>
    string[] GetInputPortNames();

    /// <summary>
    /// Opens a MIDI output port by name.
    /// Throws <see cref="InvalidOperationException"/> if the port is not found.
    /// </summary>
    IMidiOutput OpenOutput(string portName);

    /// <summary>
    /// Opens a MIDI input port by name.
    /// Throws <see cref="InvalidOperationException"/> if the port is not found.
    /// </summary>
    IMidiInput OpenInput(string portName);
}
