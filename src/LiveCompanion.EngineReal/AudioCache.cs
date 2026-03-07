using LiveCompanion.Core.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LiveCompanion.EngineReal;

/// <summary>
/// A decoded audio clip stored as interleaved IEEE-float samples in memory.
/// </summary>
public sealed class CachedAudio
{
    /// <summary>Interleaved float samples (L, R, L, R, …).</summary>
    public required float[] Samples { get; init; }

    /// <summary>Sample rate in Hz (e.g. 48000).</summary>
    public required int SampleRate { get; init; }

    /// <summary>Number of audio channels (1 = mono, 2 = stereo).</summary>
    public required int Channels { get; init; }

    /// <summary>Approximate memory consumed by <see cref="Samples"/> in bytes.</summary>
    public long MemoryBytes => (long)Samples.Length * sizeof(float);
}

/// <summary>
/// Preloads and caches audio files as PCM float arrays with LRU eviction.
/// Supports WAV, MP3 and AIFF through NAudio.
/// Thread-safe: all mutations are serialized via a lock.
/// </summary>
public sealed class AudioCache
{
    private const int TargetSampleRate = 48_000;

    /// <summary>Default memory limit: 256 MB.</summary>
    public const long DefaultMaxMemoryBytes = 256L * 1024 * 1024;

    private readonly Dictionary<string, CachedAudio> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lruOrder = new();
    private readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly ILogService _log;

    private long _totalMemoryBytes;

    public AudioCache(ILogService log, long maxMemoryBytes = DefaultMaxMemoryBytes)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        MaxMemoryBytes = maxMemoryBytes > 0 ? maxMemoryBytes : DefaultMaxMemoryBytes;
    }

    /// <summary>Maximum memory budget in bytes. Oldest entries are evicted when exceeded.</summary>
    public long MaxMemoryBytes { get; }

    /// <summary>
    /// Preloads multiple audio files in parallel. Files that fail to decode are logged and skipped.
    /// </summary>
    public async Task PreloadAsync(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var paths = filePaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

        // Filter out already-cached paths under lock
        List<string> toLoad;
        lock (_lock)
        {
            toLoad = paths.Where(p => !_cache.ContainsKey(p)).ToList();
        }

        var tasks = toLoad.Select(p => Task.Run(() => LoadFile(p)));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>Returns the cached audio for <paramref name="filePath"/>, or <c>null</c> if not cached.</summary>
    public CachedAudio? Get(string filePath)
    {
        lock (_lock)
        {
            if (!_cache.TryGetValue(filePath, out var cached))
                return null;

            // Move to front of LRU (most recently used)
            if (_lruNodes.TryGetValue(filePath, out var node))
            {
                _lruOrder.Remove(node);
                _lruOrder.AddFirst(node);
            }

            return cached;
        }
    }

    /// <summary>Removes all cached audio and reclaims memory.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruOrder.Clear();
            _lruNodes.Clear();
            _totalMemoryBytes = 0;
        }
        _log.Debug(LogSource.EngineReal, "[AudioCache] Cache cleared.");
    }

    /// <summary>Total memory consumed by all cached audio clips, in bytes.</summary>
    public long TotalMemoryBytes
    {
        get { lock (_lock) { return _totalMemoryBytes; } }
    }

    /// <summary>Number of files currently cached.</summary>
    public int Count
    {
        get { lock (_lock) { return _cache.Count; } }
    }

    // ------------------------------------------------------------------ //
    // Internal decoding
    // ------------------------------------------------------------------ //

    private void LoadFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _log.Warn(LogSource.EngineReal, $"[AudioCache] File not found: {filePath}");
                return;
            }

            using var reader = CreateReader(filePath);
            if (reader is null)
            {
                _log.Warn(LogSource.EngineReal, $"[AudioCache] Unsupported format: {filePath}");
                return;
            }

            // Convert to float samples via ISampleProvider pipeline
            ISampleProvider pipeline = reader.ToSampleProvider();

            // Resample to target sample rate if needed
            if (reader.WaveFormat.SampleRate != TargetSampleRate)
            {
                pipeline = new WdlResamplingSampleProvider(pipeline, TargetSampleRate);
            }

            // Estimate capacity from stream duration to avoid List re-allocations
            int estimatedSamples = EstimateSampleCount(reader, pipeline.WaveFormat.Channels);

            // Read all samples
            var samples = ReadAllSamples(pipeline, estimatedSamples);
            int channels = pipeline.WaveFormat.Channels;

            var cached = new CachedAudio
            {
                Samples = samples,
                SampleRate = TargetSampleRate,
                Channels = channels,
            };

            lock (_lock)
            {
                // If already loaded by another thread, skip
                if (_cache.ContainsKey(filePath))
                    return;

                _cache[filePath] = cached;
                _totalMemoryBytes += cached.MemoryBytes;

                var node = _lruOrder.AddFirst(filePath);
                _lruNodes[filePath] = node;

                Evict();
            }

            _log.Debug(LogSource.EngineReal,
                $"[AudioCache] Loaded '{Path.GetFileName(filePath)}' — " +
                $"{channels}ch, {samples.Length} samples, {cached.MemoryBytes / 1024} KB");
        }
        catch (Exception ex)
        {
            _log.Error(LogSource.EngineReal, $"[AudioCache] Failed to load '{filePath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Evicts least-recently-used entries until total memory is within <see cref="MaxMemoryBytes"/>.
    /// Must be called under <see cref="_lock"/>.
    /// </summary>
    private void Evict()
    {
        while (_totalMemoryBytes > MaxMemoryBytes && _lruOrder.Last is not null)
        {
            var oldest = _lruOrder.Last!;
            var key = oldest.Value;

            if (_cache.TryGetValue(key, out var evicted))
            {
                _totalMemoryBytes -= evicted.MemoryBytes;
                _cache.Remove(key);
            }

            _lruNodes.Remove(key);
            _lruOrder.RemoveLast();

            _log.Debug(LogSource.EngineReal,
                $"[AudioCache] Evicted '{Path.GetFileName(key)}' — " +
                $"total={_totalMemoryBytes / 1024 / 1024} MB");
        }
    }

    private static int EstimateSampleCount(WaveStream reader, int outputChannels)
    {
        var duration = reader.TotalTime;
        if (duration.TotalSeconds <= 0)
            return 0;

        // Estimate: duration * targetRate * channels, with 5% margin
        return (int)(duration.TotalSeconds * TargetSampleRate * outputChannels * 1.05);
    }

    private static WaveStream? CreateReader(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".wav" => new WaveFileReader(filePath),
            ".mp3" => new Mp3FileReader(filePath),
            ".aiff" or ".aif" => new AiffFileReader(filePath),
            _ => null,
        };
    }

    private static float[] ReadAllSamples(ISampleProvider provider, int estimatedSamples)
    {
        const int blockSize = 4096;
        var buffer = new float[blockSize];
        var capacity = estimatedSamples > blockSize ? estimatedSamples : blockSize;
        var allSamples = new List<float>(capacity);

        int read;
        while ((read = provider.Read(buffer, 0, blockSize)) > 0)
        {
            for (int i = 0; i < read; i++)
                allSamples.Add(buffer[i]);
        }

        return allSamples.ToArray();
    }
}
