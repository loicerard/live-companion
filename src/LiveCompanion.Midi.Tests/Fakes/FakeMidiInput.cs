using LiveCompanion.Midi.Abstractions;

namespace LiveCompanion.Midi.Tests.Fakes;

/// <summary>
/// Fake MIDI input port for testing. Allows tests to inject MIDI messages
/// without real MIDI hardware.
/// </summary>
public sealed class FakeMidiInput : IMidiInput
{
    private bool _started;
    private bool _disposed;

    public FakeMidiInput(string portName)
    {
        PortName = portName;
    }

    public string PortName { get; }
    public bool IsStarted => _started;
    public bool IsDisposed => _disposed;

    public event EventHandler<MidiMessageReceivedEventArgs>? MessageReceived;

    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FakeMidiInput));
        _started = true;
    }

    public void Stop()
    {
        _started = false;
    }

    public void Dispose()
    {
        _started = false;
        _disposed = true;
    }

    /// <summary>
    /// Injects a MIDI message as if it arrived from the hardware.
    /// </summary>
    public void SimulateMessage(byte status, byte data1 = 0, byte data2 = 0, long timestamp = 0)
    {
        int packed = status | (data1 << 8) | (data2 << 16);
        MessageReceived?.Invoke(this, new MidiMessageReceivedEventArgs(packed, timestamp));
    }
}
