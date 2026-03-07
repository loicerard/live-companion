using FluentAssertions;
using LiveCompanion.Core.Models;
using LiveCompanion.EngineReal;
using NAudio.Midi;
using Xunit;

namespace LiveCompanion.Tests.EngineReal;

public class MidiImportServiceRealTests : IDisposable
{
    private readonly MidiImportServiceReal _service = new();
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            if (File.Exists(f)) File.Delete(f);
        }
    }

    private string CreateTempMidiFile(Action<MidiEventCollection> configure, int deltaTicksPerQuarterNote = 480)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.mid");
        _tempFiles.Add(path);

        var events = new MidiEventCollection(1, deltaTicksPerQuarterNote);
        events.AddTrack();
        configure(events);

        // Ensure each track ends with EndTrack
        for (int i = 0; i < events.Tracks; i++)
        {
            var track = events[i];
            bool hasEndTrack = false;
            long maxTime = 0;
            foreach (var evt in track)
            {
                if (evt.AbsoluteTime > maxTime) maxTime = evt.AbsoluteTime;
                if (evt is MetaEvent me && me.MetaEventType == MetaEventType.EndTrack)
                    hasEndTrack = true;
            }
            if (!hasEndTrack)
            {
                track.Add(new MetaEvent(MetaEventType.EndTrack, 0, maxTime));
            }
        }

        MidiFile.Export(path, events);
        return path;
    }

    [Fact]
    public void Import_SimpleFile_CreatesOneSectionPlusCountdown()
    {
        // Fichier MIDI simple : tempo 120, 4/4, 8 mesures (sans marqueurs)
        var path = CreateTempMidiFile(events =>
        {
            // Tempo 120 BPM = 500000 µs/beat
            events[0].Add(new TempoEvent(500000, 0));
            events[0].Add(new TimeSignatureEvent(0, 4, 2, 24, 8));
            // Note pour donner une durée (8 mesures * 4 beats * 480 ticks = 15360)
            events[0].Add(new NoteOnEvent(0, 1, 60, 100, 0));
            events[0].Add(new NoteEvent(15360, 1, MidiCommandCode.NoteOff, 60, 0));
        });

        var result = _service.Import(path, countdownBars: 3);

        result.Sections.Should().HaveCount(2); // Décompte + 1 section
        result.Sections[0].Name.Should().Be("Décompte");
        result.Sections[0].BarCount.Should().Be(3);
        result.Sections[0].Tempo.Should().Be(120.0);
        result.Sections[0].TimeSignature.Should().Be(new TimeSignature(4, 4));

        result.Sections[1].Name.Should().Be("Section 1");
        result.Sections[1].BarCount.Should().Be(8);
    }

    [Fact]
    public void Import_WithMarkers_CreatesSectionsFromMarkers()
    {
        var path = CreateTempMidiFile(events =>
        {
            events[0].Add(new TempoEvent(500000, 0)); // 120 BPM
            events[0].Add(new TimeSignatureEvent(0, 4, 2, 24, 8)); // 4/4

            // Marqueurs : Intro (4 mesures), Couplet (8 mesures), Refrain (4 mesures)
            events[0].Add(new TextEvent("Intro", MetaEventType.Marker, 0));
            events[0].Add(new TextEvent("Couplet", MetaEventType.Marker, 4 * 4 * 480)); // tick 7680
            events[0].Add(new TextEvent("Refrain", MetaEventType.Marker, 12 * 4 * 480)); // tick 23040

            // Fin à 16 mesures
            events[0].Add(new NoteOnEvent(0, 1, 60, 100, 0));
            events[0].Add(new NoteEvent(16 * 4 * 480, 1, MidiCommandCode.NoteOff, 60, 0));
        });

        var result = _service.Import(path, countdownBars: 2);

        // 2 countdown + 3 sections
        result.Sections.Should().HaveCount(4);

        result.Sections[0].Name.Should().Be("Décompte");
        result.Sections[0].BarCount.Should().Be(2);

        result.Sections[1].Name.Should().Be("Intro");
        result.Sections[1].BarCount.Should().Be(4);

        result.Sections[2].Name.Should().Be("Couplet");
        result.Sections[2].BarCount.Should().Be(8);

        result.Sections[3].Name.Should().Be("Refrain");
        result.Sections[3].BarCount.Should().Be(4);
    }

    [Fact]
    public void Import_WithTimeSignatureChanges_ReflectsInSections()
    {
        var path = CreateTempMidiFile(events =>
        {
            events[0].Add(new TempoEvent(500000, 0)); // 120 BPM
            events[0].Add(new TimeSignatureEvent(0, 4, 2, 24, 8)); // 4/4

            // Marqueur section 1 en 4/4, 4 mesures
            events[0].Add(new TextEvent("Verse", MetaEventType.Marker, 0));

            // À la mesure 5, passage en 3/4
            long tickMeasure5 = 4 * 4 * 480; // 7680
            events[0].Add(new TimeSignatureEvent(tickMeasure5, 3, 2, 24, 8)); // 3/4
            events[0].Add(new TextEvent("Waltz", MetaEventType.Marker, tickMeasure5));

            // 4 mesures en 3/4 = 4 * 3 * 480 = 5760 ticks
            long end = tickMeasure5 + 4 * 3 * 480;
            events[0].Add(new NoteOnEvent(0, 1, 60, 100, 0));
            events[0].Add(new NoteEvent(end, 1, MidiCommandCode.NoteOff, 60, 0));
        });

        var result = _service.Import(path, countdownBars: 0);

        result.Sections.Should().HaveCount(2);

        result.Sections[0].TimeSignature.Should().Be(new TimeSignature(4, 4));
        result.Sections[0].BarCount.Should().Be(4);

        result.Sections[1].TimeSignature.Should().Be(new TimeSignature(3, 4));
        result.Sections[1].BarCount.Should().Be(4);
    }

    [Fact]
    public void Import_WithTempoChanges_ReflectsInSections()
    {
        var path = CreateTempMidiFile(events =>
        {
            events[0].Add(new TimeSignatureEvent(0, 4, 2, 24, 8)); // 4/4

            // Section 1 à 100 BPM (600000 µs/beat)
            events[0].Add(new TempoEvent(600000, 0));
            events[0].Add(new TextEvent("Slow", MetaEventType.Marker, 0));

            // Section 2 à 140 BPM (428571 µs/beat) à la mesure 5
            long tickMeasure5 = 4 * 4 * 480;
            events[0].Add(new TempoEvent(428571, tickMeasure5));
            events[0].Add(new TextEvent("Fast", MetaEventType.Marker, tickMeasure5));

            long end = tickMeasure5 + 4 * 4 * 480;
            events[0].Add(new NoteOnEvent(0, 1, 60, 100, 0));
            events[0].Add(new NoteEvent(end, 1, MidiCommandCode.NoteOff, 60, 0));
        });

        var result = _service.Import(path, countdownBars: 0);

        result.Sections.Should().HaveCount(2);
        result.Sections[0].Tempo.Should().Be(100.0);
        result.Sections[1].Tempo.Should().BeApproximately(140.0, 0.1);
    }

    [Fact]
    public void Import_NoCountdown_DoesNotAddCountdownSection()
    {
        var path = CreateTempMidiFile(events =>
        {
            events[0].Add(new TempoEvent(500000, 0));
            events[0].Add(new TimeSignatureEvent(0, 4, 2, 24, 8));
            events[0].Add(new NoteOnEvent(0, 1, 60, 100, 0));
            events[0].Add(new NoteEvent(4 * 4 * 480, 1, MidiCommandCode.NoteOff, 60, 0));
        });

        var result = _service.Import(path, countdownBars: 0);

        result.Sections.Should().HaveCount(1);
        result.Sections[0].Name.Should().NotBe("Décompte");
    }

    [Fact]
    public void Import_Title_IsFileNameWithoutExtension()
    {
        var path = CreateTempMidiFile(events =>
        {
            events[0].Add(new TempoEvent(500000, 0));
            events[0].Add(new TimeSignatureEvent(0, 4, 2, 24, 8));
            events[0].Add(new NoteOnEvent(0, 1, 60, 100, 0));
            events[0].Add(new NoteEvent(1920, 1, MidiCommandCode.NoteOff, 60, 0));
        });

        var result = _service.Import(path);

        result.Title.Should().Be(Path.GetFileNameWithoutExtension(path));
    }

    [Fact]
    public void Import_SectionOrders_AreSequential()
    {
        var path = CreateTempMidiFile(events =>
        {
            events[0].Add(new TempoEvent(500000, 0));
            events[0].Add(new TimeSignatureEvent(0, 4, 2, 24, 8));
            events[0].Add(new TextEvent("A", MetaEventType.Marker, 0));
            events[0].Add(new TextEvent("B", MetaEventType.Marker, 4 * 4 * 480));
            events[0].Add(new TextEvent("C", MetaEventType.Marker, 8 * 4 * 480));
            events[0].Add(new NoteOnEvent(0, 1, 60, 100, 0));
            events[0].Add(new NoteEvent(12 * 4 * 480, 1, MidiCommandCode.NoteOff, 60, 0));
        });

        var result = _service.Import(path, countdownBars: 1);

        // Décompte(0), A(1), B(2), C(3)
        for (int i = 0; i < result.Sections.Count; i++)
            result.Sections[i].Order.Should().Be(i);
    }

    [Fact]
    public void Import_CountdownInheritsFirstSectionTempoAndTimeSignature()
    {
        var path = CreateTempMidiFile(events =>
        {
            events[0].Add(new TempoEvent(600000, 0)); // 100 BPM
            events[0].Add(new TimeSignatureEvent(0, 3, 2, 24, 8)); // 3/4
            events[0].Add(new TextEvent("Waltz", MetaEventType.Marker, 0));
            events[0].Add(new NoteOnEvent(0, 1, 60, 100, 0));
            events[0].Add(new NoteEvent(4 * 3 * 480, 1, MidiCommandCode.NoteOff, 60, 0));
        });

        var result = _service.Import(path, countdownBars: 4);

        result.Sections[0].Name.Should().Be("Décompte");
        result.Sections[0].Tempo.Should().Be(100.0);
        result.Sections[0].TimeSignature.Should().Be(new TimeSignature(3, 4));
        result.Sections[0].BarCount.Should().Be(4);
    }

    [Fact]
    public void Import_FileNotFound_Throws()
    {
        var act = () => _service.Import("/nonexistent/file.mid");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Import_EmptyPath_Throws()
    {
        var act = () => _service.Import("");
        act.Should().Throw<ArgumentException>();
    }
}
