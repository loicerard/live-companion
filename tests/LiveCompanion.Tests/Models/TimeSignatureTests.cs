using FluentAssertions;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Validation;
using Xunit;

namespace LiveCompanion.Tests.Models;

public class TimeSignatureTests
{
    [Fact]
    public void Default_ShouldBe4_4()
    {
        var ts = TimeSignature.Default;

        ts.Numerator.Should().Be(4);
        ts.Denominator.Should().Be(4);
    }

    [Fact]
    public void Record_Equality_ShouldWork()
    {
        var ts1 = new TimeSignature(3, 4);
        var ts2 = new TimeSignature(3, 4);

        ts1.Should().Be(ts2);
    }

    [Fact]
    public void ToString_ShouldFormat()
    {
        new TimeSignature(6, 8).ToString().Should().Be("6/8");
        TimeSignature.Default.ToString().Should().Be("4/4");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void ValidDenominators_ShouldPassValidation(int denominator)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = 4, TimeSignature = new TimeSignature(4, denominator) } },
        };

        ModelValidator.ValidateSong(song).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(64)]
    public void InvalidDenominators_ShouldFailValidation(int denominator)
    {
        var song = new Song
        {
            Title = "Test",
            Sections = { new Section { Name = "S", Tempo = 120, BarCount = 4, TimeSignature = new TimeSignature(4, denominator) } },
        };

        var result = ModelValidator.ValidateSong(song);
        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Field.Contains("Denominator"));
    }

    [Fact]
    public void Inequality_ShouldWork()
    {
        var ts1 = new TimeSignature(3, 4);
        var ts2 = new TimeSignature(4, 4);

        ts1.Should().NotBe(ts2);
        (ts1 != ts2).Should().BeTrue();
    }

    [Theory]
    [InlineData(3, 4, "3/4")]
    [InlineData(6, 8, "6/8")]
    [InlineData(7, 8, "7/8")]
    [InlineData(2, 2, "2/2")]
    public void CommonSignatures_ShouldFormatCorrectly(int num, int den, string expected)
    {
        new TimeSignature(num, den).ToString().Should().Be(expected);
    }
}
