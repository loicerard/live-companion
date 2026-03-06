using FluentAssertions;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Validation;
using Xunit;

namespace LiveCompanion.Tests.Models;

public class AudioClipTests
{
    [Fact]
    public void NewAudioClip_ShouldHaveUniqueId()
    {
        var c1 = new AudioClip();
        var c2 = new AudioClip();

        c1.Id.Should().NotBe(Guid.Empty);
        c1.Id.Should().NotBe(c2.Id);
    }

    [Fact]
    public void NewAudioClip_ShouldHaveDefaults()
    {
        var clip = new AudioClip();

        clip.Name.Should().BeEmpty();
        clip.FilePath.Should().BeEmpty();
        clip.BusName.Should().Be("Main");
        clip.Volume.Should().Be(1.0);
        clip.FadeInSeconds.Should().Be(0);
        clip.FadeOutSeconds.Should().Be(0);
        clip.SyncMode.Should().Be(SyncMode.Free);
        clip.Position.Should().Be(TimelinePosition.Zero);
    }

    [Fact]
    public void AudioClip_Volume_CanBeSetToZero()
    {
        var clip = new AudioClip { Volume = 0.0 };
        clip.Volume.Should().Be(0.0);
    }

    [Fact]
    public void AudioClip_Volume_CanBeSetToMax()
    {
        var clip = new AudioClip { Volume = 1.0 };
        clip.Volume.Should().Be(1.0);
    }

    [Fact]
    public void AudioClip_Fades_CanBeSet()
    {
        var clip = new AudioClip
        {
            FadeInSeconds = 0.5,
            FadeOutSeconds = 1.0,
        };

        clip.FadeInSeconds.Should().Be(0.5);
        clip.FadeOutSeconds.Should().Be(1.0);
    }

    [Fact]
    public void AudioClip_SyncMode_BarAligned()
    {
        var clip = new AudioClip { SyncMode = SyncMode.BarAligned };
        clip.SyncMode.Should().Be(SyncMode.BarAligned);
    }

    [Fact]
    public void AudioClip_Position_CanBeCustomized()
    {
        var pos = new TimelinePosition(2, 3, 2, 0);
        var clip = new AudioClip { Position = pos };

        clip.Position.SectionIndex.Should().Be(2);
        clip.Position.Bar.Should().Be(3);
        clip.Position.Beat.Should().Be(2);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Volume_BoundaryValues_ShouldPassValidation(double volume)
    {
        var song = new Song
        {
            Title = "Test",
            AudioClips = { new AudioClip { Name = "Clip", FilePath = "/audio/clip.wav", Volume = volume } },
        };

        ModelValidator.ValidateSong(song).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Volume_OutOfRange_ShouldFailValidation(double volume)
    {
        var song = new Song
        {
            Title = "Test",
            AudioClips = { new AudioClip { Name = "Clip", FilePath = "/audio/clip.wav", Volume = volume } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Volume"));
    }

    [Fact]
    public void FadeOut_Negative_ShouldFailValidation()
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
    public void BusName_DefaultIsMain()
    {
        new AudioClip().BusName.Should().Be("Main");
    }

    [Fact]
    public void SyncMode_DefaultIsFree()
    {
        new AudioClip().SyncMode.Should().Be(SyncMode.Free);
    }
}
