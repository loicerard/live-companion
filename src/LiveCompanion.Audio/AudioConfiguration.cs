using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveCompanion.Audio;

/// <summary>
/// ASIO audio engine configuration. Serializable to/from JSON.
/// </summary>
public class AudioConfiguration
{
    public const int DefaultSampleRate = 44100;
    public const int DefaultBufferSize = 256;
    public const int DefaultMetronomeChannel = 0;
    public const int DefaultSampleChannel = 2;
    public const float DefaultMasterVolume = 1.0f;
    public const float DefaultStrongBeatVolume = 1.0f;
    public const float DefaultWeakBeatVolume = 0.7f;

    /// <summary>
    /// Name of the ASIO driver to use (e.g. "Focusrite USB ASIO").
    /// </summary>
    public string? AsioDriverName { get; set; }

    /// <summary>
    /// ASIO buffer size in samples. Lower values reduce latency but increase CPU load.
    /// </summary>
    public int BufferSize { get; set; } = DefaultBufferSize;

    /// <summary>
    /// Sample rate in Hz.
    /// </summary>
    public int SampleRate { get; set; } = DefaultSampleRate;

    /// <summary>
    /// First ASIO output channel for the metronome click (stereo pair: this and this+1).
    /// </summary>
    public int MetronomeChannelOffset { get; set; } = DefaultMetronomeChannel;

    /// <summary>
    /// First ASIO output channel for sample playback (stereo pair: this and this+1).
    /// </summary>
    public int SampleChannelOffset { get; set; } = DefaultSampleChannel;

    /// <summary>
    /// Master volume for the metronome (0.0 – 1.0).
    /// </summary>
    public float MetronomeMasterVolume { get; set; } = DefaultMasterVolume;

    /// <summary>
    /// Volume multiplier for the strong (downbeat) click (0.0 – 1.0).
    /// </summary>
    public float StrongBeatVolume { get; set; } = DefaultStrongBeatVolume;

    /// <summary>
    /// Volume multiplier for the weak beat click (0.0 – 1.0).
    /// </summary>
    public float WeakBeatVolume { get; set; } = DefaultWeakBeatVolume;

    /// <summary>
    /// Whether to attempt automatic reconnection when the ASIO driver disappears.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Delay in milliseconds between reconnection attempts.
    /// </summary>
    public int ReconnectDelayMs { get; set; } = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static AudioConfiguration FromJson(string json) =>
        JsonSerializer.Deserialize<AudioConfiguration>(json, JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize AudioConfiguration.");

    public static async Task SaveAsync(AudioConfiguration config, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions).ConfigureAwait(false);
    }

    public static async Task<AudioConfiguration> LoadAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AudioConfiguration>(stream, JsonOptions).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Failed to deserialize AudioConfiguration from {filePath}.");
    }
}
