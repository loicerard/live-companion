using LiveCompanion.Midi.Abstractions;

namespace LiveCompanion.Midi.Tests.Fakes;

/// <summary>
/// Fake MIDI output port for testing. Records all messages sent to it.
/// </summary>
public sealed class FakeMidiOutput : IMidiOutput
{
    private readonly List<int> _messages = [];
    private bool _disposed;

    public FakeMidiOutput(string portName)
    {
        PortName = portName;
    }

    public string PortName { get; }

    /// <summary>All messages sent via <see cref="Send"/>, in order.</summary>
    public IReadOnlyList<int> SentMessages => _messages;

    /// <summary>Number of Send() calls.</summary>
    public int SendCount => _messages.Count;

    /// <summary>Whether Dispose() has been called.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>If set, Send() throws this exception (simulates a fault).</summary>
    public Exception? ThrowOnSend { get; set; }

    public void Send(int packedMessage)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FakeMidiOutput));
        if (ThrowOnSend is not null)
            throw ThrowOnSend;

        _messages.Add(packedMessage);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>Returns the status byte of the Nth sent message.</summary>
    public byte GetStatus(int index) => (byte)(_messages[index] & 0xFF);

    /// <summary>Returns the first data byte of the Nth sent message.</summary>
    public byte GetData1(int index) => (byte)((_messages[index] >> 8) & 0xFF);

    /// <summary>Returns the second data byte of the Nth sent message.</summary>
    public byte GetData2(int index) => (byte)((_messages[index] >> 16) & 0xFF);

    /// <summary>Counts how many messages have the given status byte.</summary>
    public int CountWithStatus(byte status) =>
        _messages.Count(m => (m & 0xFF) == status);
}
