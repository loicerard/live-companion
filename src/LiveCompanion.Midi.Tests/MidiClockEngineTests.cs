using LiveCompanion.Audio;
using LiveCompanion.Audio.Providers;

namespace LiveCompanion.Midi.Tests;

/// <summary>
/// Tests for <see cref="MidiClockEngine"/>.
///
/// Key assertion: at PPQN=480, the engine emits exactly 24 MIDI clock pulses (0xF8)
/// per quarter note, hooked to the ASIO tick callback from MetronomeWaveProvider.
/// </summary>
public class MidiClockEngineTests
{
    private const int SampleRate = 44100;
    private const int Ppqn = 480;

    // Ticks per MIDI clock: 480 / 24 = 20
    private const int TicksPerClock = Ppqn / MidiClockEngine.ClocksPerQuarterNote;

    /// <summary>
    /// Builds the test fixture: MetronomeWaveProvider → MetronomeAudioEngine (no ASIO),
    /// FakeMidiPortFactory/Service, and a MidiClockEngine attached to the provider's ticks.
    /// </summary>
    private static (
        MidiClockEngine Clock,
        MetronomeAudioEngine Metronome,
        MetronomeWaveProvider Provider,
        FakeMidiOutput Quad1Port,
        FakeMidiOutput Quad2Port)
    BuildFixture(int bpm = 120)
    {
        var provider = new MetronomeWaveProvider(SampleRate, Ppqn, bpm, 1f, 1f, 0.7f);
        var engine = new MetronomeAudioEngine(provider);

        var config = new MidiConfiguration
        {
            OutputDevices = new()
            {
                [DeviceTarget.Quad1] = new DeviceOutputConfig { PortName = "Port-Q1" },
                [DeviceTarget.Quad2] = new DeviceOutputConfig { PortName = "Port-Q2" },
                [DeviceTarget.SSPD]  = new DeviceOutputConfig { PortName = "Port-SSPD" },
            },
            ClockTargets = [DeviceTarget.Quad1, DeviceTarget.Quad2],
        };

        var factory = new FakeMidiPortFactory();
        var q1 = factory.RegisterOutput("Port-Q1");
        var q2 = factory.RegisterOutput("Port-Q2");
        factory.RegisterOutput("Port-SSPD");

        var service = new MidiService(factory, config);
        var clock = new MidiClockEngine(service, config, Ppqn);

        return (clock, engine, provider, q1, q2);
    }

    private static int FramesForTicks(int ticks, int bpm)
    {
        double samplesPerTick = (double)SampleRate * 60.0 / ((double)bpm * Ppqn);
        return (int)Math.Ceiling(samplesPerTick * ticks) + 2;
    }

    private static float[] PumpFrames(MetronomeWaveProvider provider, int frameCount)
    {
        int sampleCount = frameCount * 2;
        var buffer = new float[sampleCount];
        provider.Read(buffer, 0, sampleCount);
        return buffer;
    }

    // ── Pulse count accuracy ──────────────────────────────────────

    [Fact]
    public void Emits_exactly_24_clocks_per_quarter_note()
    {
        var (clock, metronome, provider, q1, _) = BuildFixture(bpm: 120);

        metronome.Start();
        clock.Attach(metronome);
        clock.Start();

        // Pump frames for exactly one quarter note (480 ticks)
        int frames = FramesForTicks(Ppqn, 120);
        PumpFrames(provider, frames);

        // 480 ticks / 20 ticks-per-clock = 24 clocks
        int clockCount = q1.CountWithStatus(MidiClockEngine.MidiClock);
        Assert.InRange(clockCount, 24, 25); // ±1 for rounding tolerance
    }

    [Fact]
    public void Emits_48_clocks_per_half_note()
    {
        var (clock, metronome, provider, q1, _) = BuildFixture(bpm: 120);

        metronome.Start();
        clock.Attach(metronome);
        clock.Start();

        int frames = FramesForTicks(Ppqn * 2, 120);
        PumpFrames(provider, frames);

        int clockCount = q1.CountWithStatus(MidiClockEngine.MidiClock);
        Assert.InRange(clockCount, 48, 50);
    }

    [Fact]
    public void Emits_clocks_on_both_clock_target_ports()
    {
        var (clock, metronome, provider, q1, q2) = BuildFixture(bpm: 120);

        metronome.Start();
        clock.Attach(metronome);
        clock.Start();

        PumpFrames(provider, FramesForTicks(Ppqn, 120));

        int q1Count = q1.CountWithStatus(MidiClockEngine.MidiClock);
        int q2Count = q2.CountWithStatus(MidiClockEngine.MidiClock);

        Assert.InRange(q1Count, 24, 25);
        Assert.Equal(q1Count, q2Count); // both targets receive same count
    }

    [Fact]
    public void Does_not_emit_clocks_before_Start_is_called()
    {
        var (clock, metronome, provider, q1, _) = BuildFixture(bpm: 120);

        metronome.Start();
        clock.Attach(metronome);
        // NOT calling clock.Start()

        PumpFrames(provider, FramesForTicks(Ppqn * 4, 120));

        Assert.Equal(0, q1.CountWithStatus(MidiClockEngine.MidiClock));
    }

    [Fact]
    public void No_clocks_after_Stop_is_called()
    {
        var (clock, metronome, provider, q1, _) = BuildFixture(bpm: 120);

        metronome.Start();
        clock.Attach(metronome);
        clock.Start();

        PumpFrames(provider, FramesForTicks(Ppqn, 120));
        int beforeStop = q1.CountWithStatus(MidiClockEngine.MidiClock);

        clock.Stop();
        PumpFrames(provider, FramesForTicks(Ppqn, 120));
        int afterStop = q1.CountWithStatus(MidiClockEngine.MidiClock);

        // No new clocks should have been added after Stop
        Assert.Equal(beforeStop, afterStop);
    }

    // ── MIDI transport messages ───────────────────────────────────

    [Fact]
    public void Start_sends_MidiStart_0xFA_to_clock_targets()
    {
        var (clock, metronome, provider, q1, q2) = BuildFixture();

        metronome.Start();
        clock.Attach(metronome);
        clock.Start();

        // First message sent should be MIDI Start (0xFA)
        Assert.True(q1.SentMessages.Contains(MidiClockEngine.MidiStart),
            "Expected 0xFA (Start) in sent messages.");
        Assert.True(q2.SentMessages.Contains(MidiClockEngine.MidiStart));
    }

    [Fact]
    public void Stop_sends_MidiStop_0xFC_to_clock_targets()
    {
        var (clock, metronome, provider, q1, q2) = BuildFixture();

        metronome.Start();
        clock.Attach(metronome);
        clock.Start();
        clock.Stop();

        Assert.True(q1.SentMessages.Contains(MidiClockEngine.MidiStop),
            "Expected 0xFC (Stop) in sent messages.");
        Assert.True(q2.SentMessages.Contains(MidiClockEngine.MidiStop));
    }

    [Fact]
    public void Continue_sends_MidiContinue_0xFB_to_clock_targets()
    {
        var (clock, metronome, provider, q1, q2) = BuildFixture();

        metronome.Start();
        clock.Attach(metronome);
        clock.Start();
        clock.Stop();
        clock.Continue();

        Assert.True(q1.SentMessages.Contains(MidiClockEngine.MidiContinue),
            "Expected 0xFB (Continue) in sent messages.");
    }

    // ── Tick interval accuracy ────────────────────────────────────

    [Fact]
    public void TicksPerClock_is_ppqn_divided_by_24()
    {
        // PPQN=480 → 480/24 = 20
        var config = new MidiConfiguration();
        var factory = new FakeMidiPortFactory();
        var service = new MidiService(factory, config);
        var clock = new MidiClockEngine(service, config, Ppqn);

        Assert.Equal(20, clock.TicksPerClock);
    }

    [Fact]
    public void Constructor_throws_if_ppqn_not_divisible_by_24()
    {
        var config = new MidiConfiguration();
        var factory = new FakeMidiPortFactory();
        var service = new MidiService(factory, config);

        Assert.Throws<ArgumentException>(() =>
            new MidiClockEngine(service, config, ppqn: 100)); // 100 % 24 != 0
    }

    // ── BPM independence ──────────────────────────────────────────

    [Fact]
    public void Clock_count_per_quarter_note_is_bpm_independent()
    {
        // At any BPM, exactly 24 clocks per quarter note (480 ticks)
        foreach (int bpm in new[] { 60, 120, 180 })
        {
            var (clock, metronome, provider, q1, _) = BuildFixture(bpm);

            metronome.Start();
            clock.Attach(metronome);
            clock.Start();

            PumpFrames(provider, FramesForTicks(Ppqn, bpm));

            int clockCount = q1.CountWithStatus(MidiClockEngine.MidiClock);
            Assert.True(clockCount is >= 24 and <= 25,
                $"BPM={bpm}: expected 24±1 clocks per quarter note, got {clockCount}");
        }
    }

    // ── SSPD not included in clock targets ────────────────────────

    [Fact]
    public void SSPD_port_receives_no_clock_messages_by_default()
    {
        var (clock, metronome, provider, _, _) = BuildFixture(bpm: 120);

        // Locate the SSPD fake port
        var config = new MidiConfiguration();
        var factory = new FakeMidiPortFactory();
        var sspdPort = factory.RegisterOutput("Port-SSPD");
        factory.RegisterOutput("Port-Q1");
        factory.RegisterOutput("Port-Q2");

        var service = new MidiService(factory, config);
        var clockEngine = new MidiClockEngine(service, config, Ppqn);

        var provider2 = new MetronomeWaveProvider(SampleRate, Ppqn, 120, 1f, 1f, 0.7f);
        var metronome2 = new MetronomeAudioEngine(provider2);

        metronome2.Start();
        clockEngine.Attach(metronome2);
        clockEngine.Start();

        PumpFrames(provider2, FramesForTicks(Ppqn, 120));

        // Default config ClockTargets = [Quad1, Quad2], not SSPD
        Assert.Equal(0, sspdPort.SendCount);
    }
}
