using FluentAssertions;
using LiveCompanion.Core.Models;

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
}
