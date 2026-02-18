using NAudio.Wave;

namespace LiveCompanion.Audio.Providers;

/// <summary>
/// Multi-channel sample provider that routes multiple stereo sources
/// to specific channel-pair offsets in the output buffer.
/// Used to combine the metronome click and sample playback onto different
/// ASIO output pairs (e.g. click on channels 0-1, samples on channels 2-3).
/// </summary>
internal sealed class AsioOutputRouter : ISampleProvider
{
    private readonly int _totalChannels;
    private readonly List<(ISampleProvider Source, int ChannelOffset)> _sources = [];
    private readonly object _lock = new();

    public AsioOutputRouter(int sampleRate, int totalChannels)
    {
        if (totalChannels < 2)
            throw new ArgumentOutOfRangeException(nameof(totalChannels), "Must have at least 2 output channels.");

        _totalChannels = totalChannels;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, totalChannels);
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Registers a stereo source to be routed to the specified channel pair.
    /// </summary>
    /// <param name="source">A stereo (2-channel) sample provider.</param>
    /// <param name="channelOffset">The first output channel (0-based) for this source.</param>
    public void AddSource(ISampleProvider source, int channelOffset)
    {
        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("Source must be stereo (2 channels).", nameof(source));
        if (channelOffset < 0 || channelOffset + 1 >= _totalChannels)
            throw new ArgumentOutOfRangeException(nameof(channelOffset),
                $"Channel offset {channelOffset} is out of range for {_totalChannels} total channels.");

        lock (_lock)
        {
            _sources.Add((source, channelOffset));
        }
    }

    /// <summary>
    /// Removes all sources. Used during reconfiguration.
    /// </summary>
    public void ClearSources()
    {
        lock (_lock)
        {
            _sources.Clear();
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // Clear the output buffer
        Array.Clear(buffer, offset, count);

        int frames = count / _totalChannels;

        List<(ISampleProvider Source, int ChannelOffset)> sources;
        lock (_lock)
        {
            sources = [.. _sources];
        }

        // Temporary buffer for reading stereo data from each source
        float[] srcBuffer = new float[frames * 2];

        foreach (var (source, channelOffset) in sources)
        {
            Array.Clear(srcBuffer, 0, srcBuffer.Length);
            int read = source.Read(srcBuffer, 0, frames * 2);
            int framesRead = read / 2;

            // Mix stereo source into the correct channel pair
            for (int f = 0; f < framesRead; f++)
            {
                buffer[offset + f * _totalChannels + channelOffset] += srcBuffer[f * 2];
                buffer[offset + f * _totalChannels + channelOffset + 1] += srcBuffer[f * 2 + 1];
            }
        }

        return count;
    }
}
