using LiveCompanion.Audio.Providers;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Audio.Tests;

public class MetronomeAudioEngineTests
{
    private const int SampleRate = 44100;
    private const int Ppqn = Setlist.DefaultPpqn; // 480

    /// <summary>
    /// Creates a MetronomeWaveProvider and wraps it in MetronomeAudioEngine (test constructor).
    /// </summary>
    private static (MetronomeAudioEngine Engine, MetronomeWaveProvider Provider) CreateEngine(
        int bpm = 120, float masterVol = 1f, float strongVol = 1f, float weakVol = 0.7f)
    {
        var provider = new MetronomeWaveProvider(SampleRate, Ppqn, bpm, masterVol, strongVol, weakVol);
        var engine = new MetronomeAudioEngine(provider);
        return (engine, provider);
    }

    /// <summary>
    /// Pumps N frames through the provider by calling Read() directly.
    /// This simulates what the ASIO callback would do.
    /// </summary>
    private static float[] PumpFrames(MetronomeWaveProvider provider, int frameCount)
    {
        int sampleCount = frameCount * 2; // stereo
        var buffer = new float[sampleCount];
        provider.Read(buffer, 0, sampleCount);
        return buffer;
    }

    /// <summary>
    /// Calculates how many audio frames correspond to N ticks at the given BPM.
    /// </summary>
    private static int FramesForTicks(int ticks, int bpm)
    {
        double samplesPerTick = (double)SampleRate * 60.0 / ((double)bpm * Ppqn);
        return (int)Math.Ceiling(samplesPerTick * ticks) + 1; // +1 for accumulator rounding
    }

    [Fact]
    public void Start_sets_IsRunning()
    {
        var (engine, _) = CreateEngine();

        engine.Start();

        Assert.True(engine.IsRunning);
    }

    [Fact]
    public void Stop_clears_IsRunning()
    {
        var (engine, _) = CreateEngine();
        engine.Start();

        engine.Stop();

        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void CurrentTick_starts_at_zero()
    {
        var (engine, _) = CreateEngine();
        Assert.Equal(0, engine.CurrentTick);
    }

    [Fact]
    public void Ticks_advance_when_buffer_is_read()
    {
        var (engine, provider) = CreateEngine(bpm: 120);
        engine.Start();

        // Pump enough frames for several ticks
        // At 120 BPM, 480 PPQN: samplesPerTick = 44100 * 60 / (120 * 480) ≈ 45.9375
        // So 100 frames should give us ~2 ticks
        PumpFrames(provider, 100);

        Assert.True(engine.CurrentTick > 0, "Ticks should have advanced after pumping audio buffer.");
    }

    [Fact]
    public void TickAdvanced_fires_for_each_tick()
    {
        var (engine, provider) = CreateEngine(bpm: 120);

        var ticks = new List<long>();
        engine.TickAdvanced += t => ticks.Add(t);

        engine.Start();
        int framesToPump = FramesForTicks(10, 120);
        PumpFrames(provider, framesToPump);

        // Should have at least 10 ticks
        Assert.True(ticks.Count >= 10, $"Expected >= 10 ticks, got {ticks.Count}");

        // Ticks should be sequential
        for (int i = 1; i < ticks.Count; i++)
        {
            Assert.Equal(ticks[i - 1] + 1, ticks[i]);
        }
    }

    [Fact]
    public void Beat_fires_every_ppqn_ticks()
    {
        var (engine, provider) = CreateEngine(bpm: 120);

        var beats = new List<(int Beat, int Bar)>();
        engine.Beat += (beat, bar) => beats.Add((beat, bar));

        engine.Start();

        // Pump enough for 2 full beats (2 * PPQN = 960 ticks)
        int framesToPump = FramesForTicks(Ppqn * 2, 120);
        PumpFrames(provider, framesToPump);

        // Should have at least 2 beats
        Assert.True(beats.Count >= 2, $"Expected >= 2 beats, got {beats.Count}");
    }

    [Fact]
    public void Beat_reports_correct_beat_and_bar_numbers_in_4_4()
    {
        var (engine, provider) = CreateEngine(bpm: 120);

        var beats = new List<(int Beat, int Bar)>();
        engine.Beat += (beat, bar) => beats.Add((beat, bar));

        engine.Start();

        // Pump enough for 8 beats (2 full bars of 4/4)
        int framesToPump = FramesForTicks(Ppqn * 8, 120);
        PumpFrames(provider, framesToPump);

        Assert.True(beats.Count >= 8, $"Expected >= 8 beats, got {beats.Count}");

        // In 4/4: beats should cycle 0,1,2,3,0,1,2,3,...
        // First beat at tick 480: totalBeats=1, beat=1, bar=0
        // Beat at tick 960: totalBeats=2, beat=2, bar=0
        // Beat at tick 1440: totalBeats=3, beat=3, bar=0
        // Beat at tick 1920: totalBeats=4, beat=0, bar=1  (start of bar 2)
        Assert.Equal(1, beats[0].Beat);
        Assert.Equal(0, beats[0].Bar);

        Assert.Equal(2, beats[1].Beat);
        Assert.Equal(0, beats[1].Bar);

        Assert.Equal(3, beats[2].Beat);
        Assert.Equal(0, beats[2].Bar);

        Assert.Equal(0, beats[3].Beat); // downbeat of bar 2
        Assert.Equal(1, beats[3].Bar);
    }

    [Fact]
    public void Beat_reports_correct_beat_numbers_in_3_4()
    {
        var (engine, provider) = CreateEngine(bpm: 120);
        engine.ChangeTempo(120, new TimeSignature(3, 4));

        var beats = new List<(int Beat, int Bar)>();
        engine.Beat += (beat, bar) => beats.Add((beat, bar));

        engine.Start();

        // Pump enough for 6 beats (2 bars of 3/4)
        int framesToPump = FramesForTicks(Ppqn * 6, 120);
        PumpFrames(provider, framesToPump);

        Assert.True(beats.Count >= 6, $"Expected >= 6 beats, got {beats.Count}");

        // In 3/4: beats cycle 0,1,2,0,1,2,...
        // First beat (tick 480): totalBeats=1, beat=1, bar=0
        // tick 960: totalBeats=2, beat=2, bar=0
        // tick 1440: totalBeats=3, beat=0, bar=1 (start of bar 2)
        Assert.Equal(1, beats[0].Beat);
        Assert.Equal(0, beats[0].Bar);

        Assert.Equal(2, beats[1].Beat);
        Assert.Equal(0, beats[1].Bar);

        Assert.Equal(0, beats[2].Beat); // downbeat of bar 2
        Assert.Equal(1, beats[2].Bar);
    }

    [Fact]
    public void ChangeTempo_affects_tick_rate()
    {
        var (engine, provider) = CreateEngine(bpm: 60);

        engine.Start();

        // At 60 BPM: samplesPerTick = 44100 * 60 / (60 * 480) ≈ 91.875
        // Pump 1000 frames at 60 BPM
        PumpFrames(provider, 1000);
        long ticksAt60 = engine.CurrentTick;

        // Reset and try at 120 BPM
        engine.Reset();
        engine.ChangeTempo(120, TimeSignature.Common);

        PumpFrames(provider, 1000);
        long ticksAt120 = engine.CurrentTick;

        // At 120 BPM, ticks should advance roughly twice as fast
        Assert.True(ticksAt120 > ticksAt60,
            $"At 120 BPM ({ticksAt120} ticks) should be more than at 60 BPM ({ticksAt60} ticks)");

        // With tolerance: should be approximately 2x
        double ratio = (double)ticksAt120 / ticksAt60;
        Assert.InRange(ratio, 1.8, 2.2);
    }

    [Fact]
    public void Reset_sets_tick_to_zero()
    {
        var (engine, provider) = CreateEngine(bpm: 120);
        engine.Start();
        PumpFrames(provider, 500);

        Assert.True(engine.CurrentTick > 0);

        engine.Reset();

        Assert.Equal(0, engine.CurrentTick);
    }

    [Fact]
    public void No_ticks_advance_when_stopped()
    {
        var (engine, provider) = CreateEngine(bpm: 120);
        // Don't call Start() — engine should output silence

        PumpFrames(provider, 1000);

        Assert.Equal(0, engine.CurrentTick);
    }

    [Fact]
    public void Audio_output_is_stereo()
    {
        var (_, provider) = CreateEngine();
        Assert.Equal(2, provider.WaveFormat.Channels);
    }

    [Fact]
    public void Click_tone_is_generated_on_beat_boundary()
    {
        var (engine, provider) = CreateEngine(bpm: 120, masterVol: 1f, strongVol: 1f, weakVol: 1f);
        engine.Start();

        // Pump past the first beat (tick 480)
        int framesToBeat = FramesForTicks(Ppqn, 120);
        var buffer = PumpFrames(provider, framesToBeat + 200);

        // There should be non-zero samples in the buffer (the click tone)
        bool hasNonZero = false;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (Math.Abs(buffer[i]) > 0.001f)
            {
                hasNonZero = true;
                break;
            }
        }

        Assert.True(hasNonZero, "Expected click audio to be generated at beat boundary.");
    }

    [Fact]
    public void Silence_when_not_running()
    {
        var (_, provider) = CreateEngine(bpm: 120);
        // Not started

        var buffer = PumpFrames(provider, 500);

        // All samples should be zero
        for (int i = 0; i < buffer.Length; i++)
        {
            Assert.Equal(0f, buffer[i]);
        }
    }

    [Fact]
    public void Tick_count_is_accurate_over_one_beat()
    {
        var (engine, provider) = CreateEngine(bpm: 120);
        engine.Start();

        // Pump exactly the number of frames for PPQN ticks
        // At 120 BPM: samplesPerTick = 44100 * 60 / (120 * 480) = 45.9375
        // For 480 ticks: 480 * 45.9375 = 22050 frames
        double samplesPerTick = (double)SampleRate * 60.0 / (120.0 * Ppqn);
        int framesForOneBeat = (int)(samplesPerTick * Ppqn);
        PumpFrames(provider, framesForOneBeat);

        // With floating-point accumulation, we should be very close to PPQN
        // Allow ±1 tick tolerance
        Assert.InRange(engine.CurrentTick, Ppqn - 1, Ppqn + 1);
    }
}
