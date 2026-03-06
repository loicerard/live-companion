using FluentAssertions;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Validation;

namespace LiveCompanion.Tests.Validation;

public class ModelValidatorTests
{
    // ------------------------------------------------------------------ //
    // ValidateSong — cas valide
    // ------------------------------------------------------------------ //

    [Fact]
    public void ValidateSong_ValidSong_ShouldBeValid()
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "Intro", Tempo = 120, BarCount = 4, TimeSignature = TimeSignature.Default } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSong_EmptySectionsAndClips_ShouldBeValid()
    {
        var song = new Song { Title = "Minimal" };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeTrue();
    }

    // ------------------------------------------------------------------ //
    // ValidateSong — Title
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateSong_EmptyTitle_ShouldReturnError(string? title)
    {
        var song = new Song { Title = title! };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field == "Title");
    }

    // ------------------------------------------------------------------ //
    // ValidateSong — Section
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    [InlineData(301)]
    [InlineData(-10)]
    public void ValidateSong_TempoOutOfRange_ShouldReturnError(double tempo)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "Bad", Tempo = tempo, BarCount = 4, TimeSignature = TimeSignature.Default } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Tempo"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateSong_BarCountZeroOrNegative_ShouldReturnError(int barCount)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "Bad", Tempo = 120, BarCount = barCount, TimeSignature = TimeSignature.Default } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("BarCount"));
    }

    [Fact]
    public void ValidateSong_InvalidTimeSignatureNumerator_ShouldReturnError()
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "Bad", Tempo = 120, BarCount = 4, TimeSignature = new TimeSignature(0, 4) } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Numerator"));
    }

    [Fact]
    public void ValidateSong_InvalidTimeSignatureDenominator_ShouldReturnError()
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "Bad", Tempo = 120, BarCount = 4, TimeSignature = new TimeSignature(4, 5) } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Denominator"));
    }

    // ------------------------------------------------------------------ //
    // ValidateSong — AudioClip
    // ------------------------------------------------------------------ //

    [Fact]
    public void ValidateSong_EmptyAudioClipPath_ShouldReturnError()
    {
        var song = new Song
        {
            Title = "Test",
            AudioClips = { new AudioClip { Name = "Drums", FilePath = "" } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("FilePath"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ValidateSong_VolumeOutOfRange_ShouldReturnError(double volume)
    {
        var song = new Song
        {
            Title = "Test",
            AudioClips = { new AudioClip { Name = "Drums", FilePath = "/audio/drums.wav",
                Sends = [new BusSend { BusName = "Main", Volume = volume }] } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Volume"));
    }

    [Fact]
    public void ValidateSong_NegativeFadeIn_ShouldReturnError()
    {
        var song = new Song
        {
            Title = "Test",
            AudioClips = { new AudioClip { Name = "Drums", FilePath = "/audio/drums.wav", FadeInSeconds = -1 } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("FadeIn"));
    }

    // ------------------------------------------------------------------ //
    // ValidateSong — MidiEvent
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(-1)]
    public void ValidateSong_MidiChannelOutOfRange_ShouldReturnError(int channel)
    {
        var song = new Song
        {
            Title = "Test",
            MidiEvents = { new MidiEvent { Channel = channel } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Channel"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void ValidateSong_MidiData1OutOfRange_ShouldReturnError(int data1)
    {
        var song = new Song
        {
            Title = "Test",
            MidiEvents = { new MidiEvent { Channel = 1, Data1 = data1 } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Data1"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void ValidateSong_MidiData2OutOfRange_ShouldReturnError(int data2)
    {
        var song = new Song
        {
            Title = "Test",
            MidiEvents = { new MidiEvent { Channel = 1, Data2 = data2 } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Data2"));
    }

    // ------------------------------------------------------------------ //
    // ValidateSong — erreurs multiples
    // ------------------------------------------------------------------ //

    [Fact]
    public void ValidateSong_MultipleErrors_ShouldAccumulate()
    {
        var song = new Song
        {
            Title = "",
            Sections = { new Section { Name = "Bad", Tempo = 0, BarCount = 0, TimeSignature = TimeSignature.Default } },
            AudioClips = { new AudioClip { Name = "X", FilePath = "",
                Sends = [new BusSend { BusName = "Main", Volume = 2.0 }] } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().HaveCountGreaterOrEqualTo(4);
    }

    // ------------------------------------------------------------------ //
    // ValidateSongFiles
    // ------------------------------------------------------------------ //

    [Fact]
    public void ValidateSongFiles_MissingFile_ShouldReturnWarning()
    {
        var song = new Song
        {
            Title = "Test",
            AudioClips = { new AudioClip { Name = "Ghost", FilePath = "/nonexistent/audio.wav" } },
        };

        var result = ModelValidator.ValidateSongFiles(song);

        result.IsValid.Should().BeTrue(); // Warnings ne bloquent pas
        result.HasWarnings.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Field.Contains("FilePath") && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void ValidateSongFiles_MissingClickTrack_ShouldReturnWarning()
    {
        var song = new Song
        {
            Title = "Test",
            ClickTrackPath = "/nonexistent/click.wav",
        };

        var result = ModelValidator.ValidateSongFiles(song);

        result.HasWarnings.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Field == "ClickTrackPath");
    }

    [Fact]
    public void ValidateSongFiles_NoFiles_ShouldBeClean()
    {
        var song = new Song { Title = "Test" };

        var result = ModelValidator.ValidateSongFiles(song);

        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeFalse();
    }

    // ------------------------------------------------------------------ //
    // ValidatePlaylists
    // ------------------------------------------------------------------ //

    [Fact]
    public void ValidatePlaylists_AllSongsExist_ShouldBeValid()
    {
        var song = new Song { Title = "A" };
        var playlist = new Playlist { Name = "Set", SongIds = { song.Id } };

        var result = ModelValidator.ValidatePlaylists([playlist], [song]);

        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeFalse();
    }

    [Fact]
    public void ValidatePlaylists_MissingSong_ShouldReturnWarning()
    {
        var playlist = new Playlist { Name = "Set", SongIds = { Guid.NewGuid() } };

        var result = ModelValidator.ValidatePlaylists([playlist], []);

        result.IsValid.Should().BeTrue(); // Warning, pas erreur
        result.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public void ValidatePlaylists_EmptyName_ShouldReturnError()
    {
        var playlist = new Playlist { Name = "" };

        var result = ModelValidator.ValidatePlaylists([playlist], []);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Name"));
    }

    // ------------------------------------------------------------------ //
    // ValidateSong — cas limites valides
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(20)]
    [InlineData(120)]
    [InlineData(300)]
    public void ValidateSong_ValidTempoBoundaries_ShouldPass(double tempo)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = tempo, BarCount = 4, TimeSignature = TimeSignature.Default } },
        };

        ModelValidator.ValidateSong(song).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateSong_NegativeFadeOut_ShouldReturnError()
    {
        var song = new Song
        {
            Title = "Test",
            AudioClips = { new AudioClip { Name = "Clip", FilePath = "/audio/clip.wav", FadeOutSeconds = -1 } },
        };

        var result = ModelValidator.ValidateSong(song);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("FadeOut"));
    }

    [Fact]
    public void ValidateSong_ValidMidiValues_ShouldPass()
    {
        var song = new Song
        {
            Title = "Test",
            MidiEvents = { new MidiEvent { Channel = 1, Data1 = 0, Data2 = 127 } },
        };

        ModelValidator.ValidateSong(song).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(3, 4)]
    [InlineData(6, 8)]
    [InlineData(7, 8)]
    [InlineData(4, 16)]
    public void ValidateSong_ValidTimeSignatures_ShouldPass(int num, int den)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = 4, TimeSignature = new TimeSignature(num, den) } },
        };

        ModelValidator.ValidateSong(song).IsValid.Should().BeTrue();
    }

    // ------------------------------------------------------------------ //
    // Null guards
    // ------------------------------------------------------------------ //

    [Fact]
    public void ValidateSong_NullSong_ShouldThrow()
    {
        var act = () => ModelValidator.ValidateSong(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateSongFiles_NullSong_ShouldThrow()
    {
        var act = () => ModelValidator.ValidateSongFiles(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidatePlaylists_NullPlaylists_ShouldThrow()
    {
        var act = () => ModelValidator.ValidatePlaylists(null!, []);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidatePlaylists_NullSongs_ShouldThrow()
    {
        var act = () => ModelValidator.ValidatePlaylists([], null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
