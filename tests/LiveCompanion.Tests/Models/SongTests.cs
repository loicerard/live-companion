using FluentAssertions;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Tests.Models;

public class SongTests
{
    [Fact]
    public void NewSong_ShouldHaveUniqueId()
    {
        var song1 = new Song();
        var song2 = new Song();

        song1.Id.Should().NotBe(Guid.Empty);
        song2.Id.Should().NotBe(Guid.Empty);
        song1.Id.Should().NotBe(song2.Id);
    }

    [Fact]
    public void NewSong_ShouldHaveEmptyTitle()
    {
        var song = new Song();
        song.Title.Should().BeEmpty();
    }

    [Fact]
    public void NewSong_ShouldHaveEmptyCollections()
    {
        var song = new Song();

        song.Sections.Should().BeEmpty();
        song.AudioClips.Should().BeEmpty();
        song.MidiEvents.Should().BeEmpty();
    }

    [Fact]
    public void NewSong_ClickTrackPath_ShouldBeNull()
    {
        var song = new Song();
        song.ClickTrackPath.Should().BeNull();
    }

    [Fact]
    public void NewSong_LastModified_ShouldBeRecentUtc()
    {
        var before = DateTime.UtcNow;
        var song = new Song();
        var after = DateTime.UtcNow;

        song.LastModified.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Song_CanAddSections()
    {
        var song = new Song { Title = "Test Song" };
        var section = new Section { Name = "Intro", Tempo = 130, BarCount = 8 };

        song.Sections.Add(section);

        song.Sections.Should().HaveCount(1);
        song.Sections[0].Name.Should().Be("Intro");
    }

    [Fact]
    public void Song_CanAddAudioClips()
    {
        var song = new Song();
        var clip = new AudioClip { Name = "Kick", FilePath = "/audio/kick.wav" };

        song.AudioClips.Add(clip);

        song.AudioClips.Should().HaveCount(1);
        song.AudioClips[0].Name.Should().Be("Kick");
    }

    [Fact]
    public void Song_CanAddMidiEvents()
    {
        var song = new Song();
        var evt = new MidiEvent { Type = MidiEventType.ProgramChange, Channel = 1, Data1 = 42 };

        song.MidiEvents.Add(evt);

        song.MidiEvents.Should().HaveCount(1);
        song.MidiEvents[0].Data1.Should().Be(42);
    }
}
