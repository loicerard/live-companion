using System.Diagnostics;
using LiveCompanion.Core.Models;
using NAudio.Wave;

namespace LiveCompanion.Audio.Providers;

/// <summary>
/// Stereo sample provider that generates metronome click tones and advances
/// the tick counter. Designed to be driven by the ASIO callback — the tick
/// counter advances as audio samples are produced, making the audio buffer
/// the single source of timing truth.
/// </summary>
internal sealed class MetronomeWaveProvider : ISampleProvider
{
    // Click tone parameters — bursts must stay within 10–20 ms so they are
    // heard as discrete clicks and never bleed into a continuous tone.
    private const float StrongBeatFrequencyHz = 1000f;
    private const float WeakBeatFrequencyHz   = 800f;
    private const float StrongBeatDurationMs  = 15f;   // was 30 ms — reduced to ≤20 ms spec
    private const float WeakBeatDurationMs    = 10f;   // was 20 ms — reduced to 10 ms

    private readonly int _ppqn;
    private readonly int _sampleRate;
    private readonly object _lock = new();

    private int _bpm;
    private TimeSignature _timeSignature = TimeSignature.Common;
    private float _masterVolume;
    private float _strongBeatVolume;
    private float _weakBeatVolume;
    private bool _running;

    // Tick tracking — fractional accumulator for sub-sample precision
    private double _sampleAccumulator;
    private long _currentTick;

    // Click tone generation state
    private float _currentClickFrequency;
    private int _clickSamplesRemaining;
    private int _clickSampleIndex;
    private int _clickTotalSamples;    // full duration of the current burst (for envelope)

    public MetronomeWaveProvider(int sampleRate, int ppqn, int initialBpm,
        float masterVolume, float strongBeatVolume, float weakBeatVolume)
    {
        _sampleRate = sampleRate;
        _ppqn = ppqn;
        _bpm = initialBpm;
        _masterVolume = masterVolume;
        _strongBeatVolume = strongBeatVolume;
        _weakBeatVolume = weakBeatVolume;

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2); // stereo
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Current tick counter, advanced by the audio callback.</summary>
    public long CurrentTick
    {
        get { lock (_lock) return _currentTick; }
    }

    public bool IsRunning
    {
        get { lock (_lock) return _running; }
    }

    /// <summary>Fired on every tick advance. Parameter: current tick.</summary>
    public event Action<long>? TickAdvanced;

    /// <summary>Fired on every beat boundary. Parameters: beat (0-based in bar), bar (0-based).</summary>
    public event Action<int, int>? Beat;

    public void Start()
    {
        lock (_lock)
        {
            _running = true;
            _currentTick = 0;
            _sampleAccumulator = 0;
            _clickSamplesRemaining = 0;
            _clickSampleIndex = 0;
            _clickTotalSamples = 0;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _running = false;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _currentTick = 0;
            _sampleAccumulator = 0;
            _clickSamplesRemaining = 0;
            _clickSampleIndex = 0;
            _clickTotalSamples = 0;
        }
    }

    public void ChangeTempo(int bpm, TimeSignature timeSignature)
    {
        lock (_lock)
        {
            _bpm = bpm;
            _timeSignature = timeSignature;
        }
    }

    public void SetVolumes(float masterVolume, float strongBeatVolume, float weakBeatVolume)
    {
        lock (_lock)
        {
            _masterVolume = masterVolume;
            _strongBeatVolume = strongBeatVolume;
            _weakBeatVolume = weakBeatVolume;
        }
    }

    /// <summary>
    /// Called by the ASIO callback (via the output router). Fills the buffer with
    /// stereo click audio and advances the tick counter.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesWritten = 0;
        int frames = count / 2; // stereo → frame count

        for (int frame = 0; frame < frames; frame++)
        {
            float sample;
            bool running;
            int bpm, ppqn, sampleRate;
            float masterVol, strongVol, weakVol;
            TimeSignature ts;

            lock (_lock)
            {
                running = _running;
                bpm = _bpm;
                ppqn = _ppqn;
                sampleRate = _sampleRate;
                masterVol = _masterVolume;
                strongVol = _strongBeatVolume;
                weakVol = _weakBeatVolume;
                ts = _timeSignature;
            }

            if (!running)
            {
                // Output silence when not running
                buffer[offset + frame * 2] = 0f;
                buffer[offset + frame * 2 + 1] = 0f;
                samplesWritten += 2;
                continue;
            }

            // Calculate samples per tick: SampleRate * 60 / (BPM * PPQN)
            double samplesPerTick = (double)sampleRate * 60.0 / ((double)bpm * ppqn);

            // Accumulate samples and check for tick boundaries
            _sampleAccumulator += 1.0;

            while (_sampleAccumulator >= samplesPerTick)
            {
                _sampleAccumulator -= samplesPerTick;

                long newTick;
                lock (_lock)
                {
                    _currentTick++;
                    newTick = _currentTick;
                }

                TickAdvanced?.Invoke(newTick);

                // Check for beat boundary (every PPQN ticks)
                if (newTick % ppqn == 0)
                {
                    var totalBeats = (int)(newTick / ppqn);
                    var beatsPerBar = ts.Numerator;
                    var bar = totalBeats / beatsPerBar;
                    var beat = totalBeats % beatsPerBar;

                    // Determine click parameters
                    bool isStrongBeat = beat == 0;
                    float freq = isStrongBeat ? StrongBeatFrequencyHz : WeakBeatFrequencyHz;
                    float durationMs = isStrongBeat ? StrongBeatDurationMs : WeakBeatDurationMs;
                    float beatVolume = isStrongBeat ? strongVol : weakVol;

                    int burstSamples = (int)(sampleRate * durationMs / 1000f);
                    lock (_lock)
                    {
                        _currentClickFrequency = freq;
                        _clickSamplesRemaining = burstSamples;
                        _clickTotalSamples     = burstSamples;
                        _clickSampleIndex = 0;
                    }

                    // Log the exact burst duration for diagnostics (Bug 1)
                    Debug.WriteLine(
                        $"[Metronome] Beat {beat} bar {bar} — {(isStrongBeat ? "STRONG" : "weak")} " +
                        $"burst {durationMs:F0} ms = {burstSamples} samples @ {sampleRate} Hz");

                    Beat?.Invoke(beat, bar);
                }
            }

            // Generate audio sample
            int clickRemaining;
            float clickFreq;
            int clickIdx;
            lock (_lock)
            {
                clickRemaining = _clickSamplesRemaining;
                clickFreq = _currentClickFrequency;
                clickIdx = _clickSampleIndex;
            }

            if (clickRemaining > 0)
            {
                // Determine volume based on frequency (strong vs weak)
                bool isStrong = Math.Abs(clickFreq - StrongBeatFrequencyHz) < 1f;
                float beatVol = isStrong ? strongVol : weakVol;

                int totalSamples;
                lock (_lock) { totalSamples = _clickTotalSamples; }

                // Linear fade-out envelope using the stored total burst length
                // (avoids re-computing the duration constant on every sample)
                float envelope = totalSamples > 0
                    ? (float)clickRemaining / totalSamples
                    : 0f;
                sample = MathF.Sin(2f * MathF.PI * clickFreq * clickIdx / sampleRate)
                         * envelope * masterVol * beatVol;

                lock (_lock)
                {
                    _clickSamplesRemaining--;
                    _clickSampleIndex++;
                }
            }
            else
            {
                sample = 0f;
            }

            // Write stereo (same sample to both channels)
            buffer[offset + frame * 2] = sample;
            buffer[offset + frame * 2 + 1] = sample;
            samplesWritten += 2;
        }

        return samplesWritten;
    }
}
