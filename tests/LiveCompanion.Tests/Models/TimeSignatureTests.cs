using FluentAssertions;
using LiveCompanion.Core.Models;
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
}
