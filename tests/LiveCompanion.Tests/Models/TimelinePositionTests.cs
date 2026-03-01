using FluentAssertions;
using LiveCompanion.Core.Models;

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
}
