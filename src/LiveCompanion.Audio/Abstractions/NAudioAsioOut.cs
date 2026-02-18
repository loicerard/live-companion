using NAudio.Wave;

namespace LiveCompanion.Audio.Abstractions;

/// <summary>
/// Real wrapper around NAudio's <see cref="AsioOut"/>.
/// </summary>
public sealed class NAudioAsioOut : IAsioOut
{
    private readonly AsioOut _asio;

    public NAudioAsioOut(string driverName)
    {
        _asio = new AsioOut(driverName);
        _asio.PlaybackStopped += (s, e) => PlaybackStopped?.Invoke(this, e);
    }

    public string DriverName => _asio.DriverName;
    public int NumberOfOutputChannels => _asio.DriverOutputChannelCount;
    public PlaybackState PlaybackState => _asio.PlaybackState;

    public int ChannelOffset
    {
        get => _asio.ChannelOffset;
        set => _asio.ChannelOffset = value;
    }

    public void Init(ISampleProvider sampleProvider) => _asio.Init(sampleProvider);
    public void Play() => _asio.Play();
    public void Stop() => _asio.Stop();

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public void Dispose() => _asio.Dispose();
}
