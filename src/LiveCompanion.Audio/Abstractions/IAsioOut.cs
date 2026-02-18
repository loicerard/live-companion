using NAudio.Wave;

namespace LiveCompanion.Audio.Abstractions;

/// <summary>
/// Abstraction over NAudio's <see cref="NAudio.Wave.AsioOut"/> for testability.
/// Real implementation wraps AsioOut; tests use a fake that simulates ASIO callbacks.
/// </summary>
public interface IAsioOut : IDisposable
{
    string DriverName { get; }
    int NumberOfOutputChannels { get; }
    PlaybackState PlaybackState { get; }
    int ChannelOffset { get; set; }

    void Init(ISampleProvider sampleProvider);
    void Play();
    void Stop();

    /// <summary>
    /// Fired when playback stops, either normally or due to an error.
    /// A non-null <see cref="StoppedEventArgs.Exception"/> indicates a fault.
    /// </summary>
    event EventHandler<StoppedEventArgs>? PlaybackStopped;
}
