using FluentAssertions;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Validation;
using Xunit;

namespace LiveCompanion.Tests.Models;

public class SectionTests
{
    [Fact]
    public void NewSection_ShouldHaveUniqueId()
    {
        var s1 = new Section();
        var s2 = new Section();

        s1.Id.Should().NotBe(Guid.Empty);
        s1.Id.Should().NotBe(s2.Id);
    }

    [Fact]
    public void NewSection_ShouldHaveDefaults()
    {
        var section = new Section();

        section.Name.Should().BeEmpty();
        section.Tempo.Should().Be(120.0);
        section.TimeSignature.Should().Be(TimeSignature.Default);
        section.BarCount.Should().Be(4);
        section.Order.Should().Be(0);
    }

    [Fact]
    public void Section_Tempo_CanBeSetWithinRange()
    {
        var section = new Section { Tempo = 20 };
        section.Tempo.Should().Be(20);

        section.Tempo = 300;
        section.Tempo.Should().Be(300);
    }

    [Fact]
    public void Section_BarCount_MustBePositive()
    {
        var section = new Section { BarCount = 1 };
        section.BarCount.Should().Be(1);
    }

    [Fact]
    public void Section_CanSetCustomTimeSignature()
    {
        var section = new Section
        {
            TimeSignature = new TimeSignature(6, 8)
        };

        section.TimeSignature.Numerator.Should().Be(6);
        section.TimeSignature.Denominator.Should().Be(8);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(200)]
    [InlineData(300)]
    public void Tempo_ValidRange_ShouldPassValidation(double tempo)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = tempo, BarCount = 4, TimeSignature = TimeSignature.Default } },
        };

        ModelValidator.ValidateSong(song).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19.9)]
    [InlineData(300.1)]
    [InlineData(-1)]
    public void Tempo_OutOfRange_ShouldFailValidation(double tempo)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = tempo, BarCount = 4, TimeSignature = TimeSignature.Default } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Tempo"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(100)]
    public void BarCount_ValidValues_ShouldPassValidation(int barCount)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = barCount, TimeSignature = TimeSignature.Default } },
        };

        ModelValidator.ValidateSong(song).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BarCount_Invalid_ShouldFailValidation(int barCount)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = barCount, TimeSignature = TimeSignature.Default } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("BarCount"));
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(6, 8)]
    [InlineData(7, 8)]
    public void Section_WithVariousTimeSignatures(int num, int den)
    {
        var section = new Section
        {
            TimeSignature = new TimeSignature(num, den)
        };

        section.TimeSignature.Numerator.Should().Be(num);
        section.TimeSignature.Denominator.Should().Be(den);
    }
}
