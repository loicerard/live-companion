using FluentAssertions;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Tests.Models;

public class MidiEventTests
{
    [Fact]
    public void NewMidiEvent_ShouldHaveDefaults()
    {
        var evt = new MidiEvent();

        evt.Id.Should().NotBe(Guid.Empty);
        evt.Type.Should().Be(MidiEventType.ProgramChange);
        evt.DeviceOut.Should().BeEmpty();
        evt.Channel.Should().Be(1);
        evt.Data1.Should().Be(0);
        evt.Data2.Should().Be(0);
        evt.Position.Should().Be(TimelinePosition.Zero);
    }

    [Fact]
    public void MidiEvent_CanSetAllProperties()
    {
        var pos = new TimelinePosition(1, 2, 3, 0);
        var evt = new MidiEvent
        {
            Type = MidiEventType.ControlChange,
            DeviceOut = "MockMIDI Port 1",
            Channel = 10,
            Data1 = 7,
            Data2 = 100,
            Position = pos,
        };

        evt.Type.Should().Be(MidiEventType.ControlChange);
        evt.DeviceOut.Should().Be("MockMIDI Port 1");
        evt.Channel.Should().Be(10);
        evt.Data1.Should().Be(7);
        evt.Data2.Should().Be(100);
        evt.Position.Should().Be(pos);
    }

    [Fact]
    public void MidiEvent_TwoEvents_HaveDifferentIds()
    {
        var e1 = new MidiEvent();
        var e2 = new MidiEvent();

        e1.Id.Should().NotBe(e2.Id);
    }
}
