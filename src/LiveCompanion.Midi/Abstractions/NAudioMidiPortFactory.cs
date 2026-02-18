using NAudio.Midi;

namespace LiveCompanion.Midi.Abstractions;

/// <summary>
/// NAudio-backed implementation of <see cref="IMidiPortFactory"/>.
/// Enumerates system MIDI ports and opens them via NAudio's MidiOut/MidiIn.
/// </summary>
public sealed class NAudioMidiPortFactory : IMidiPortFactory
{
    public string[] GetOutputPortNames()
    {
        var names = new string[MidiOut.NumberOfDevices];
        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            names[i] = MidiOut.DeviceInfo(i).ProductName;
        return names;
    }

    public string[] GetInputPortNames()
    {
        var names = new string[MidiIn.NumberOfDevices];
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            names[i] = MidiIn.DeviceInfo(i).ProductName;
        return names;
    }

    public IMidiOutput OpenOutput(string portName)
    {
        int index = FindOutputIndex(portName);
        if (index < 0)
            throw new InvalidOperationException($"MIDI output port '{portName}' not found.");
        return new NAudioMidiOutput(portName, new MidiOut(index));
    }

    public IMidiInput OpenInput(string portName)
    {
        int index = FindInputIndex(portName);
        if (index < 0)
            throw new InvalidOperationException($"MIDI input port '{portName}' not found.");
        return new NAudioMidiInput(portName, new MidiIn(index));
    }

    private static int FindOutputIndex(string portName)
    {
        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            if (string.Equals(MidiOut.DeviceInfo(i).ProductName, portName,
                    StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static int FindInputIndex(string portName)
    {
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            if (string.Equals(MidiIn.DeviceInfo(i).ProductName, portName,
                    StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}
