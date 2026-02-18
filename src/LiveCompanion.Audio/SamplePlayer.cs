using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LiveCompanion.Audio;

/// <summary>
/// Plays audio samples in response to <see cref="AudioCueEvent"/>s.
/// Subscribes to <see cref="SetlistPlayer.AudioCueFired"/> and plays the
/// referenced audio files on the configured ASIO channel pair.
///
/// Samples are pre-loaded into memory when a setlist is loaded, avoiding
/// disk I/O during live performance.
/// </summary>
public sealed class SamplePlayer : IDisposable
{
    private readonly AsioService? _asioService;
    private readonly int _channelOffset;
    private readonly int _sampleRate;
    private readonly MixingSampleProvider _mixer;
    private readonly Dictionary<string, float[]> _sampleCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _disposed;

    public SamplePlayer(AsioService asioService, AudioConfiguration config)
    {
        _asioService = asioService ?? throw new ArgumentNullException(nameof(asioService));
        _channelOffset = config.SampleChannelOffset;
        _sampleRate = config.SampleRate;

        var mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, 2); // stereo
        _mixer = new MixingSampleProvider(mixerFormat) { ReadFully = true };

        // Register as a source on the ASIO output router
        _asioService.RegisterSource(_mixer, _channelOffset);
    }

    /// <summary>
    /// Constructor for testing — no AsioService, mixer is accessible directly.
    /// </summary>
    internal SamplePlayer(int sampleRate)
    {
        _asioService = null;
        _channelOffset = 0;
        _sampleRate = sampleRate;

        var mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, 2);
        _mixer = new MixingSampleProvider(mixerFormat) { ReadFully = true };
    }

    /// <summary>Number of samples currently loaded in memory.</summary>
    public int LoadedSampleCount
    {
        get { lock (_lock) return _sampleCache.Count; }
    }

    /// <summary>The internal mixer, for testing and direct reads.</summary>
    internal MixingSampleProvider Mixer => _mixer;

    /// <summary>
    /// Pre-loads all audio files referenced by the setlist into memory.
    /// Call this when loading a setlist, before playback starts.
    /// </summary>
    /// <param name="setlist">The setlist whose audio cues to load.</param>
    /// <param name="samplesDirectory">Root directory containing the sample files.</param>
    public void LoadSetlistSamples(Setlist setlist, string samplesDirectory)
    {
        if (setlist is null) throw new ArgumentNullException(nameof(setlist));
        if (samplesDirectory is null) throw new ArgumentNullException(nameof(samplesDirectory));

        lock (_lock)
        {
            _sampleCache.Clear();
        }

        // Collect all unique sample file names from the setlist
        var sampleFileNames = setlist.Songs
            .SelectMany(s => s.Events)
            .OfType<AudioCueEvent>()
            .Select(e => e.SampleFileName)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var fileName in sampleFileNames)
        {
            var fullPath = Path.Combine(samplesDirectory, fileName);
            if (!File.Exists(fullPath))
                continue;

            var samples = LoadAudioFile(fullPath);
            lock (_lock)
            {
                _sampleCache[fileName] = samples;
            }
        }
    }

    /// <summary>
    /// Manually loads a single sample file into the cache.
    /// Useful for testing or dynamic sample loading.
    /// </summary>
    internal void LoadSample(string fileName, float[] pcmData)
    {
        lock (_lock)
        {
            _sampleCache[fileName] = pcmData;
        }
    }

    /// <summary>
    /// Subscribes to a <see cref="SetlistPlayer"/>'s AudioCueFired event.
    /// </summary>
    public void SubscribeTo(SetlistPlayer player)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));
        player.AudioCueFired += OnAudioCueFired;
    }

    /// <summary>
    /// Unsubscribes from a <see cref="SetlistPlayer"/>'s AudioCueFired event.
    /// </summary>
    public void UnsubscribeFrom(SetlistPlayer player)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));
        player.AudioCueFired -= OnAudioCueFired;
    }

    /// <summary>
    /// Handles an audio cue event — triggers playback of the referenced sample.
    /// Can also be called directly for testing.
    /// </summary>
    internal void OnAudioCueFired(AudioCueEvent cue)
    {
        if (cue is null) return;

        float[] pcmData;
        lock (_lock)
        {
            if (!_sampleCache.TryGetValue(cue.SampleFileName, out pcmData!))
                return; // Sample not loaded, skip silently
        }

        // Calculate linear gain from dB
        float gain = (float)Math.Pow(10.0, cue.GainDb / 20.0);

        // Create a sample provider from the cached PCM data
        var sampleProvider = new CachedSampleProvider(pcmData, _sampleRate, gain);
        _mixer.AddMixerInput(sampleProvider);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _sampleCache.Clear();
        }
    }

    private float[] LoadAudioFile(string filePath)
    {
        using var reader = new AudioFileReader(filePath);

        // Resample to our target sample rate if needed
        ISampleProvider source = reader;
        if (reader.WaveFormat.SampleRate != _sampleRate)
        {
            var resampler = new WdlResamplingSampleProvider(reader, _sampleRate);
            source = resampler;
        }

        // Convert to stereo if mono
        if (source.WaveFormat.Channels == 1)
        {
            source = new MonoToStereoSampleProvider(source);
        }

        // Read all samples into memory
        var allSamples = new List<float>();
        var buffer = new float[4096];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
                allSamples.Add(buffer[i]);
        }

        return allSamples.ToArray();
    }

    /// <summary>
    /// Plays back cached PCM float data as a stereo sample provider.
    /// Applies a gain multiplier and auto-removes from the mixer when done.
    /// </summary>
    private sealed class CachedSampleProvider : ISampleProvider
    {
        private readonly float[] _data;
        private readonly float _gain;
        private int _position;

        public CachedSampleProvider(float[] data, int sampleRate, float gain)
        {
            _data = data;
            _gain = gain;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int remaining = _data.Length - _position;
            int toCopy = Math.Min(count, remaining);

            for (int i = 0; i < toCopy; i++)
            {
                buffer[offset + i] = _data[_position + i] * _gain;
            }

            _position += toCopy;

            // Zero-fill the rest if we've run out of data
            if (toCopy < count)
            {
                Array.Clear(buffer, offset + toCopy, count - toCopy);
            }

            return toCopy;
        }
    }
}
