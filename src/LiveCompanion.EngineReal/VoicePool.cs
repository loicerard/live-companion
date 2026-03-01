using LiveCompanion.Core.Interfaces;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Manages a pool of 16 pre-allocated audio voices for simultaneous PCM playback.
/// Each voice reads from a <see cref="CachedAudio"/> buffer, applies volume and
/// fade-in/fade-out ramping, and mixes into a named bus.
/// <para>
/// Thread-safe: all mutations are serialized within a <c>lock</c>.
/// </para>
/// </summary>
public sealed class VoicePool
{
    /// <summary>Maximum number of simultaneous voices.</summary>
    public const int MaxVoices = 16;

    private readonly Voice[] _voices;
    private readonly object _lock = new();
    private readonly ILogService _log;

    public VoicePool(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _voices = new Voice[MaxVoices];
        for (int i = 0; i < MaxVoices; i++)
            _voices[i] = new Voice();
    }

    // ------------------------------------------------------------------ //
    // Properties
    // ------------------------------------------------------------------ //

    /// <summary>Number of currently active (playing) voices.</summary>
    public int ActiveCount
    {
        get
        {
            lock (_lock)
            {
                int count = 0;
                for (int i = 0; i < MaxVoices; i++)
                    if (_voices[i].IsActive) count++;
                return count;
            }
        }
    }

    // ------------------------------------------------------------------ //
    // Voice allocation
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Tries to allocate a free voice and start playing the given audio.
    /// Returns <c>true</c> if a voice was allocated, <c>false</c> if the pool is full.
    /// </summary>
    public bool TryAllocate(
        CachedAudio audio,
        string busName,
        float volume,
        double fadeInSeconds,
        double fadeOutSeconds)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentException.ThrowIfNullOrEmpty(busName);

        lock (_lock)
        {
            for (int i = 0; i < MaxVoices; i++)
            {
                if (!_voices[i].IsActive)
                {
                    _voices[i].Start(audio, busName, volume, fadeInSeconds, fadeOutSeconds);
                    _log.Debug(LogSource.EngineReal,
                        $"[VoicePool] Voice {i} allocated — bus='{busName}', vol={volume:F2}");
                    return true;
                }
            }
        }

        _log.Warn(LogSource.EngineReal,
            $"[VoicePool] Voice limit ({MaxVoices}) reached — allocation dropped.");
        return false;
    }

    /// <summary>Stops all active voices immediately.</summary>
    public void StopAll()
    {
        lock (_lock)
        {
            for (int i = 0; i < MaxVoices; i++)
                _voices[i].Stop();
        }

        _log.Debug(LogSource.EngineReal, "[VoicePool] StopAll — all voices released.");
    }

    // ------------------------------------------------------------------ //
    // Mixing
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Fills per-bus stereo output buffers by mixing all active voices.
    /// Buffers are cleared before mixing. Voices that finish playing are auto-released.
    /// </summary>
    /// <param name="busBuffers">
    /// Dictionary mapping bus names to stereo buffer pairs (Left, Right).
    /// Each buffer must have at least <paramref name="sampleCount"/> elements.
    /// </param>
    /// <param name="sampleCount">Number of samples to produce per channel.</param>
    public void FillBuffers(Dictionary<string, (float[] Left, float[] Right)> busBuffers, int sampleCount)
    {
        // Clear all bus buffers
        foreach (var (_, (left, right)) in busBuffers)
        {
            Array.Clear(left, 0, sampleCount);
            Array.Clear(right, 0, sampleCount);
        }

        lock (_lock)
        {
            for (int v = 0; v < MaxVoices; v++)
            {
                var voice = _voices[v];
                if (!voice.IsActive) continue;

                if (!busBuffers.TryGetValue(voice.BusName, out var buffers))
                    continue; // bus not mapped — skip silently

                voice.MixInto(buffers.Left, buffers.Right, sampleCount);
            }
        }
    }

    // ------------------------------------------------------------------ //
    // Fade computation (public for testability)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Computes the combined fade-in/fade-out gain at the given position.
    /// Returns a value between 0.0 and 1.0.
    /// </summary>
    public static float ComputeFadeGain(int position, int totalSamples, int fadeInSamples, int fadeOutSamples)
    {
        float gain = 1.0f;

        // Fade-in ramp
        if (fadeInSamples > 0 && position < fadeInSamples)
            gain *= (float)position / fadeInSamples;

        // Fade-out ramp
        if (fadeOutSamples > 0)
        {
            int fadeOutStart = totalSamples - fadeOutSamples;
            if (position >= fadeOutStart)
                gain *= (float)(totalSamples - position) / fadeOutSamples;
        }

        return gain;
    }
}

/// <summary>
/// A single audio voice that reads PCM samples from a <see cref="CachedAudio"/> buffer,
/// applies volume and fade ramping, and mixes into stereo output buffers.
/// </summary>
internal sealed class Voice
{
    private CachedAudio? _audio;
    private int _readPosition;  // per-channel sample position
    private string _busName = string.Empty;
    private float _volume;
    private int _fadeInSamples;
    private int _fadeOutSamples;
    private int _totalSamples;  // total per-channel sample count

    /// <summary>Whether this voice is currently playing.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The bus this voice is routed to.</summary>
    public string BusName => _busName;

    /// <summary>Starts the voice with the given parameters.</summary>
    public void Start(
        CachedAudio audio,
        string busName,
        float volume,
        double fadeInSeconds,
        double fadeOutSeconds)
    {
        _audio = audio;
        _busName = busName;
        _volume = Math.Clamp(volume, 0f, 1f);
        _readPosition = 0;
        _totalSamples = audio.Samples.Length / audio.Channels;
        _fadeInSamples = (int)(fadeInSeconds * audio.SampleRate);
        _fadeOutSamples = (int)(fadeOutSeconds * audio.SampleRate);
        IsActive = true;
    }

    /// <summary>Stops the voice and releases its audio reference.</summary>
    public void Stop()
    {
        IsActive = false;
        _audio = null;
        _readPosition = 0;
    }

    /// <summary>
    /// Mixes this voice's audio into the given stereo buffers (additive).
    /// Advances the read position and auto-stops when the audio ends.
    /// </summary>
    public void MixInto(float[] left, float[] right, int sampleCount)
    {
        if (!IsActive || _audio is null) return;

        var samples = _audio.Samples;
        int channels = _audio.Channels;

        for (int i = 0; i < sampleCount; i++)
        {
            if (_readPosition >= _totalSamples)
            {
                Stop();
                return;
            }

            float gain = _volume * VoicePool.ComputeFadeGain(_readPosition, _totalSamples, _fadeInSamples, _fadeOutSamples);

            if (channels >= 2)
            {
                int idx = _readPosition * 2;
                left[i] += samples[idx] * gain;
                right[i] += samples[idx + 1] * gain;
            }
            else
            {
                // Mono → duplicate to both channels
                float sample = samples[_readPosition] * gain;
                left[i] += sample;
                right[i] += sample;
            }

            _readPosition++;
        }

        // Auto-release when we've reached the end of the audio
        if (_readPosition >= _totalSamples)
            Stop();
    }

}
