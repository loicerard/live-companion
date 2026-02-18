using NAudio.Midi;

namespace LiveCompanion.Midi.Abstractions;

/// <summary>
/// NAudio-backed implementation of <see cref="IMidiOutput"/>.
/// Wraps <see cref="MidiOut"/> with the shared abstraction.
/// </summary>
internal sealed class NAudioMidiOutput : IMidiOutput
{
    private readonly MidiOut _midiOut;
    private bool _disposed;

    public NAudioMidiOutput(string portName, MidiOut midiOut)
    {
        PortName = portName;
        _midiOut = midiOut;
    }

    public string PortName { get; }

    public void Send(int packedMessage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _midiOut.Send(packedMessage);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _midiOut.Dispose();
    }
}
