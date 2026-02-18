using LiveCompanion.Audio.Abstractions;
using NAudio.Wave;

namespace LiveCompanion.Audio.Tests.Fakes;

/// <summary>
/// Fake ASIO output for testing. Simulates ASIO callback behavior
/// by allowing tests to manually pump audio buffers through the registered
/// sample provider.
/// </summary>
internal sealed class FakeAsioOut : IAsioOut
{
    private ISampleProvider? _provider;
    private bool _disposed;

    public FakeAsioOut(string driverName, int outputChannels = 4)
    {
        DriverName = driverName;
        NumberOfOutputChannels = outputChannels;
    }

    public string DriverName { get; }
    public int NumberOfOutputChannels { get; }
    public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
    public int ChannelOffset { get; set; }

    /// <summary>Number of times Play() was called.</summary>
    public int PlayCallCount { get; private set; }

    /// <summary>Number of times Stop() was called.</summary>
    public int StopCallCount { get; private set; }

    /// <summary>The sample provider registered via Init().</summary>
    public ISampleProvider? RegisteredProvider => _provider;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public void Init(ISampleProvider sampleProvider)
    {
        _provider = sampleProvider;
    }

    public void Play()
    {
        PlayCallCount++;
        PlaybackState = PlaybackState.Playing;
    }

    public void Stop()
    {
        StopCallCount++;
        PlaybackState = PlaybackState.Stopped;
    }

    /// <summary>
    /// Simulates an ASIO buffer callback by reading samples from the registered provider.
    /// Returns the buffer that was filled.
    /// </summary>
    public float[] PumpBuffer(int frameCount)
    {
        if (_provider is null)
            throw new InvalidOperationException("No provider registered. Call Init() first.");

        int sampleCount = frameCount * _provider.WaveFormat.Channels;
        var buffer = new float[sampleCount];
        _provider.Read(buffer, 0, sampleCount);
        return buffer;
    }

    /// <summary>
    /// Simulates a fault (e.g. ASIO device disconnected).
    /// </summary>
    public void SimulateFault(Exception exception)
    {
        PlaybackState = PlaybackState.Stopped;
        PlaybackStopped?.Invoke(this, new StoppedEventArgs(exception));
    }

    /// <summary>
    /// Simulates a normal playback stop.
    /// </summary>
    public void SimulateNormalStop()
    {
        PlaybackState = PlaybackState.Stopped;
        PlaybackStopped?.Invoke(this, new StoppedEventArgs());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PlaybackState = PlaybackState.Stopped;
    }
}
