using FluentAssertions;
using LiveCompanion.Core.Models;
using Xunit;

namespace LiveCompanion.Tests.Models;

public class TimelinePositionTests
{
    [Fact]
    public void Zero_ShouldBeAtStart()
    {
        var zero = TimelinePosition.Zero;

        zero.SectionIndex.Should().Be(0);
        zero.Bar.Should().Be(1);
        zero.Beat.Should().Be(1);
        zero.Tick.Should().Be(0);
    }

    [Fact]
    public void Record_Equality_ShouldWork()
    {
        var pos1 = new TimelinePosition(0, 1, 1, 0);
        var pos2 = new TimelinePosition(0, 1, 1, 0);

        pos1.Should().Be(pos2);
        (pos1 == pos2).Should().BeTrue();
    }

    [Fact]
    public void Record_Inequality_ShouldWork()
    {
        var pos1 = new TimelinePosition(0, 1, 1, 0);
        var pos2 = new TimelinePosition(0, 1, 2, 0);

        pos1.Should().NotBe(pos2);
        (pos1 != pos2).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var pos = new TimelinePosition(0, 2, 3, 42);
        pos.ToString().Should().Be("S1 | 2:3:042");
    }

    [Fact]
    public void ToString_Zero_ShouldFormatCorrectly()
    {
        TimelinePosition.Zero.ToString().Should().Be("S1 | 1:1:000");
    }

    [Fact]
    public void With_ShouldCreateModifiedCopy()
    {
        var pos = new TimelinePosition(1, 2, 3, 0);
        var modified = pos with { Beat = 4 };

        modified.SectionIndex.Should().Be(1);
        modified.Bar.Should().Be(2);
        modified.Beat.Should().Be(4);
        pos.Beat.Should().Be(3); // original unchanged
    }

    [Fact]
    public void Tick_ShouldBeStoredCorrectly()
    {
        var pos = new TimelinePosition(0, 1, 1, 480);

        pos.Tick.Should().Be(480);
    }

    [Fact]
    public void DifferentSections_ShouldNotBeEqual()
    {
        var pos1 = new TimelinePosition(0, 1, 1, 0);
        var pos2 = new TimelinePosition(1, 1, 1, 0);

        pos1.Should().NotBe(pos2);
        (pos1 != pos2).Should().BeTrue();
    }

    [Fact]
    public void ToString_WithLargeTick_ShouldPadCorrectly()
    {
        var pos = new TimelinePosition(0, 1, 1, 123);
        pos.ToString().Should().Be("S1 | 1:1:123");

        var pos2 = new TimelinePosition(0, 1, 1, 7);
        pos2.ToString().Should().Be("S1 | 1:1:007");
    }

    [Fact]
    public void With_ShouldModifyTick()
    {
        var pos = new TimelinePosition(0, 1, 1, 0);
        var modified = pos with { Tick = 240 };

        modified.Tick.Should().Be(240);
        pos.Tick.Should().Be(0); // original unchanged
    }

    [Fact]
    public void Deconstruct_ShouldWork()
    {
        var pos = new TimelinePosition(2, 3, 4, 120);
        var (sectionIndex, bar, beat, tick) = pos;

        sectionIndex.Should().Be(2);
        bar.Should().Be(3);
        beat.Should().Be(4);
        tick.Should().Be(120);
    }
}
