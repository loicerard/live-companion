using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Tests;

/// <summary>
/// Builds realistic setlists for unit tests.
/// </summary>
internal static class TestSetlistFactory
{
    /// <summary>
    /// Two-song setlist:
    ///   Song 1 — "Riders" (2 sections, 1 audio cue, presets for all 3 devices)
    ///   Song 2 — "Closer" (1 section, simple)
    /// </summary>
    public static Setlist CreateTwoSongSetlist()
    {
        const int ppqn = Setlist.DefaultPpqn; // 480

        var song1 = new Song
        {
            Title = "Riders on the Storm",
            Artist = "The Doors",
            DurationTicks = ppqn * 4 * 8, // 8 bars of 4 beats = 15360 ticks
            Events =
            [
                new SectionChangeEvent
                {
                    Tick = 0,
                    SectionName = "Intro",
                    Bpm = 110,
                    TimeSignature = TimeSignature.Common,
                    Presets =
                    [
                        new MidiPreset { Device = DeviceTarget.Quad1, Channel = 1, ProgramChange = 12, ControlChanges = [new(1, 64)] },
                        new MidiPreset { Device = DeviceTarget.Quad2, Channel = 2, ProgramChange = 5 },
                        new MidiPreset { Device = DeviceTarget.SSPD, Channel = 10, ProgramChange = 0 }
                    ]
                },
                new AudioCueEvent
                {
                    Tick = ppqn * 4, // beat 4 (start of bar 2)
                    SampleFileName = "rain-loop.wav",
                    GainDb = -3.0
                },
                new SectionChangeEvent
                {
                    Tick = ppqn * 4 * 4, // bar 5
                    SectionName = "Verse",
                    Bpm = 120,
                    TimeSignature = TimeSignature.Common,
                    Presets =
                    [
                        new MidiPreset { Device = DeviceTarget.Quad1, Channel = 1, ProgramChange = 14 },
                        new MidiPreset { Device = DeviceTarget.Quad2, Channel = 2, ProgramChange = 7, ControlChanges = [new(7, 100)] },
                        new MidiPreset { Device = DeviceTarget.SSPD, Channel = 10, ProgramChange = 1 }
                    ]
                }
            ]
        };

        var song2 = new Song
        {
            Title = "Closer",
            Artist = "Nine Inch Nails",
            DurationTicks = ppqn * 4 * 4, // 4 bars = 7680 ticks
            Events =
            [
                new SectionChangeEvent
                {
                    Tick = 0,
                    SectionName = "Main",
                    Bpm = 95,
                    TimeSignature = TimeSignature.Common,
                    Presets =
                    [
                        new MidiPreset { Device = DeviceTarget.Quad1, Channel = 1, ProgramChange = 20 },
                        new MidiPreset { Device = DeviceTarget.Quad2, Channel = 2, ProgramChange = 20 }
                    ]
                }
            ]
        };

        return new Setlist
        {
            Name = "Test Gig",
            Ppqn = ppqn,
            Songs = [song1, song2]
        };
    }

    /// <summary>Single-song setlist for simple tests.</summary>
    public static Setlist CreateSingleSongSetlist()
    {
        const int ppqn = Setlist.DefaultPpqn;
        return new Setlist
        {
            Name = "Soundcheck",
            Ppqn = ppqn,
            Songs =
            [
                new Song
                {
                    Title = "Test Tone",
                    Artist = "Engineer",
                    DurationTicks = ppqn * 4 * 2, // 2 bars
                    Events =
                    [
                        new SectionChangeEvent
                        {
                            Tick = 0,
                            SectionName = "Full",
                            Bpm = 120,
                            TimeSignature = TimeSignature.Common,
                            Presets = [new MidiPreset { Device = DeviceTarget.Quad1, Channel = 1, ProgramChange = 1 }]
                        }
                    ]
                }
            ]
        };
    }
}
