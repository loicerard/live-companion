using System.Collections.Concurrent;
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
/// Preloads and caches audio files as PCM float arrays.
/// Supports WAV, MP3 and AIFF through NAudio.
/// Thread-safe: backed by <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class AudioCache
{
    private const int TargetSampleRate = 48_000;

    private readonly ConcurrentDictionary<string, CachedAudio> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogService _log;

    public AudioCache(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Preloads multiple audio files in parallel. Files that fail to decode are logged and skipped.
    /// </summary>
    public async Task PreloadAsync(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var tasks = filePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Where(p => !_cache.ContainsKey(p))
            .Select(p => Task.Run(() => LoadFile(p)));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>Returns the cached audio for <paramref name="filePath"/>, or <c>null</c> if not cached.</summary>
    public CachedAudio? Get(string filePath) =>
        _cache.TryGetValue(filePath, out var cached) ? cached : null;

    /// <summary>Removes all cached audio and reclaims memory.</summary>
    public void Clear()
    {
        _cache.Clear();
        _log.Debug(LogSource.EngineReal, "[AudioCache] Cache cleared.");
    }

    /// <summary>Total memory consumed by all cached audio clips, in bytes.</summary>
    public long TotalMemoryBytes => _cache.Values.Sum(c => c.MemoryBytes);

    /// <summary>Number of files currently cached.</summary>
    public int Count => _cache.Count;

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

            // Read all samples
            var samples = ReadAllSamples(pipeline);
            int channels = pipeline.WaveFormat.Channels;

            var cached = new CachedAudio
            {
                Samples = samples,
                SampleRate = TargetSampleRate,
                Channels = channels,
            };

            _cache[filePath] = cached;
            _log.Debug(LogSource.EngineReal,
                $"[AudioCache] Loaded '{Path.GetFileName(filePath)}' — " +
                $"{channels}ch, {samples.Length} samples, {cached.MemoryBytes / 1024} KB");
        }
        catch (Exception ex)
        {
            _log.Error(LogSource.EngineReal, $"[AudioCache] Failed to load '{filePath}': {ex.Message}");
        }
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

    private static float[] ReadAllSamples(ISampleProvider provider)
    {
        const int blockSize = 4096;
        var buffer = new float[blockSize];
        var allSamples = new List<float>();

        int read;
        while ((read = provider.Read(buffer, 0, blockSize)) > 0)
        {
            if (read == blockSize)
                allSamples.AddRange(buffer);
            else
                allSamples.AddRange(buffer.AsSpan(0, read).ToArray());
        }

        return allSamples.ToArray();
    }
}
