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
        clip.Sends.Should().HaveCount(1);
        clip.Sends[0].BusName.Should().Be("Main");
        clip.Sends[0].Volume.Should().Be(1.0);
        clip.FadeInSeconds.Should().Be(0);
        clip.FadeOutSeconds.Should().Be(0);
        clip.SyncMode.Should().Be(SyncMode.Free);
        clip.Position.Should().Be(TimelinePosition.Zero);
    }

    [Fact]
    public void AudioClip_SendVolume_CanBeSetToZero()
    {
        var clip = new AudioClip { Sends = [new BusSend { Volume = 0.0 }] };
        clip.Sends[0].Volume.Should().Be(0.0);
    }

    [Fact]
    public void AudioClip_SendVolume_CanBeSetToMax()
    {
        var clip = new AudioClip { Sends = [new BusSend { Volume = 1.0 }] };
        clip.Sends[0].Volume.Should().Be(1.0);
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
            AudioClips = { new AudioClip { Name = "Clip", FilePath = "/audio/clip.wav",
                Sends = [new BusSend { BusName = "Main", Volume = volume }] } },
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
            AudioClips = { new AudioClip { Name = "Clip", FilePath = "/audio/clip.wav",
                Sends = [new BusSend { BusName = "Main", Volume = volume }] } },
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
    public void Sends_DefaultIsMainBus()
    {
        var clip = new AudioClip();
        clip.Sends.Should().HaveCount(1);
        clip.Sends[0].BusName.Should().Be("Main");
    }

    [Fact]
    public void SyncMode_DefaultIsFree()
    {
        new AudioClip().SyncMode.Should().Be(SyncMode.Free);
    }

    [Fact]
    public void MultipleSends_ShouldBeSupported()
    {
        var clip = new AudioClip
        {
            Sends =
            [
                new BusSend { BusName = "Main", Volume = 0.8 },
                new BusSend { BusName = "Click", Volume = 0.3 },
                new BusSend { BusName = "FX", Volume = 0.5 },
            ]
        };

        clip.Sends.Should().HaveCount(3);
        clip.Sends[1].BusName.Should().Be("Click");
        clip.Sends[1].Volume.Should().Be(0.3);
    }

    [Fact]
    public void EmptySends_ShouldFailValidation()
    {
        var song = new Song
        {
            Title = "Test",
            AudioClips = { new AudioClip { Name = "Clip", FilePath = "/audio/clip.wav", Sends = [] } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Sends"));
    }

    [Fact]
    public void MigrateLegacyFields_ShouldConvertBusNameToSend()
    {
        var clip = new AudioClip();
        clip.LegacyBusName = "Click";
        clip.LegacyVolume = 0.75;

        clip.MigrateLegacyFields();

        clip.Sends.Should().HaveCount(1);
        clip.Sends[0].BusName.Should().Be("Click");
        clip.Sends[0].Volume.Should().Be(0.75);
        clip.LegacyBusName.Should().BeNull();
        clip.LegacyVolume.Should().BeNull();
    }

    [Fact]
    public void MigrateLegacyFields_NoLegacy_ShouldKeepDefaults()
    {
        var clip = new AudioClip();
        clip.MigrateLegacyFields();

        clip.Sends.Should().HaveCount(1);
        clip.Sends[0].BusName.Should().Be("Main");
        clip.Sends[0].Volume.Should().Be(1.0);
    }
}
