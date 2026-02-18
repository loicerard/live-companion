using NAudio.Midi;

namespace LiveCompanion.Midi.Abstractions;

/// <summary>
/// NAudio-backed implementation of <see cref="IMidiInput"/>.
/// Wraps <see cref="MidiIn"/> with the shared abstraction.
/// </summary>
internal sealed class NAudioMidiInput : IMidiInput
{
    private readonly MidiIn _midiIn;
    private bool _disposed;

    public NAudioMidiInput(string portName, MidiIn midiIn)
    {
        PortName = portName;
        _midiIn = midiIn;
        _midiIn.MessageReceived += OnMessageReceived;
    }

    public string PortName { get; }

    public event EventHandler<MidiMessageReceivedEventArgs>? MessageReceived;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _midiIn.Start();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _midiIn.Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _midiIn.MessageReceived -= OnMessageReceived;
        _midiIn.Dispose();
    }

    private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        MessageReceived?.Invoke(this,
            new MidiMessageReceivedEventArgs(e.RawMessage, e.Timestamp));
    }
}
