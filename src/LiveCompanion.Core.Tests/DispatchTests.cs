using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Tests;

public class DispatchTests
{
    private MetronomeEngine CreateMetronome() => new(Setlist.DefaultPpqn, 120);

    [Fact]
    public void SectionChanged_fires_at_correct_tick()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        var setlist = TestSetlistFactory.CreateTwoSongSetlist();
        player.Load(setlist);

        var sections = new List<string>();
        player.SectionChanged += section => sections.Add(section.SectionName);

        player.PlaySynchronous();

        // Song1: Intro (tick 0) + Verse (tick 1920) ; Song2: Main (tick 0)
        Assert.Contains("Intro", sections);
        Assert.Contains("Verse", sections);
        Assert.Contains("Main", sections);
        Assert.Equal(3, sections.Count);
    }

    [Fact]
    public void AudioCueFired_fires_at_correct_tick()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        player.Load(TestSetlistFactory.CreateTwoSongSetlist());

        var cues = new List<string>();
        player.AudioCueFired += cue => cues.Add(cue.SampleFileName);

        player.PlaySynchronous();

        Assert.Single(cues);
        Assert.Equal("rain-loop.wav", cues[0]);
    }

    [Fact]
    public void MidiPresetChanged_fires_for_all_presets_in_section()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        player.Load(TestSetlistFactory.CreateTwoSongSetlist());

        var presets = new List<MidiPreset>();
        player.MidiPresetChanged += preset => presets.Add(preset);

        player.PlaySynchronous();

        // Song1 Intro: 3 presets + Song1 Verse: 3 presets + Song2 Main: 2 presets = 8
        Assert.Equal(8, presets.Count);
    }

    [Fact]
    public void Beat_fires_on_quarter_note_boundaries()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        player.Load(TestSetlistFactory.CreateSingleSongSetlist());

        var beats = new List<(int Beat, int Bar)>();
        player.BeatFired += (beat, bar) => beats.Add((beat, bar));

        player.PlaySynchronous();

        // 2 bars of 4/4 = 8 beats
        Assert.Equal(8, beats.Count);

        // First beat should be (1,0) — tick 480 is beat index 1 of bar 0
        // because tick 0 is not hit (AdvanceTick goes 1..N)
        // Actually: metronome resets to 0, then advances 1..3840
        // Beat fires when tick % ppqn == 0: ticks 480, 960, 1440, 1920, 2400, 2880, 3360, 3840
        Assert.Equal((1, 0), beats[0]); // tick 480 → totalBeats=1, bar=0, beat=1
    }

    [Fact]
    public void Events_are_not_dispatched_after_stop()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        var setlist = TestSetlistFactory.CreateTwoSongSetlist();
        player.Load(setlist);

        int sectionCount = 0;
        player.SectionChanged += _ => sectionCount++;

        // Stop after first song starts — only the Intro section of song 1 should fire
        player.SongStarted += (_, idx) =>
        {
            if (idx == 0)
            {
                // Let a few ticks pass then request stop
                // Actually, stop right after song 1's first section fires
            }
        };

        // More precise: stop after the first section fires
        bool shouldStop = false;
        player.SectionChanged += _ =>
        {
            if (sectionCount == 1)
                shouldStop = true;
        };

        // Use a wrapper approach — request stop via the synchronous flag
        player.SectionChanged += _ =>
        {
            if (shouldStop)
                player.SynchronousStopRequested = true;
        };

        player.PlaySynchronous();

        // Should have fired ≤ 2 sections (Intro triggers stop flag, Verse may or may not fire
        // depending on exact ordering, but definitely not 3 which would include song 2)
        Assert.True(sectionCount < 3, $"Expected < 3 sections but got {sectionCount}");
    }

    [Fact]
    public void Midi_presets_contain_correct_device_targets()
    {
        var metronome = CreateMetronome();
        var player = new SetlistPlayer(metronome);
        player.Load(TestSetlistFactory.CreateTwoSongSetlist());

        var devices = new List<DeviceTarget>();
        player.MidiPresetChanged += preset => devices.Add(preset.Device);

        player.PlaySynchronous();

        Assert.Contains(DeviceTarget.Quad1, devices);
        Assert.Contains(DeviceTarget.Quad2, devices);
        Assert.Contains(DeviceTarget.SSPD, devices);
    }
}
