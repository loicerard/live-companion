using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineMock;
using Xunit;

namespace LiveCompanion.Tests.Mocks;

public class MidiInputServiceMockTests
{
    private readonly ILogService _log = new DebugLogService();

    private MidiInputServiceMock CreateService() => new(_log);

    private static List<MidiTransportMap> BasicMappings => [
        new() { Action = TransportAction.Play,    EventType = MidiEventType.ControlChange, Data1 = 64 },
        new() { Action = TransportAction.Stop,    EventType = MidiEventType.ControlChange, Data1 = 65 },
        new() { Action = TransportAction.NextSection,   EventType = MidiEventType.NoteOn, Data1 = 48 },
        new() { Action = TransportAction.PreviousSong, EventType = MidiEventType.NoteOn, Data1 = 49 },
        new() { Action = TransportAction.NextSong,     EventType = MidiEventType.NoteOn, Data1 = 50 },
    ];

    // ------------------------------------------------------------------ //
    // GetAvailableInputPorts
    // ------------------------------------------------------------------ //

    [Fact]
    public void GetAvailableInputPorts_ShouldReturnFakePorts()
    {
        var svc = CreateService();
        svc.GetAvailableInputPorts().Should().HaveCount(2);
        svc.GetAvailableInputPorts().Should().Contain("MockMIDI IN 1");
    }

    // ------------------------------------------------------------------ //
    // SimulateInput — matching mappings
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(MidiEventType.ControlChange, 1, 64, TransportAction.Play)]
    [InlineData(MidiEventType.ControlChange, 1, 65, TransportAction.Stop)]
    [InlineData(MidiEventType.NoteOn, 1, 48, TransportAction.NextSection)]
    [InlineData(MidiEventType.NoteOn, 1, 49, TransportAction.PreviousSong)]
    [InlineData(MidiEventType.NoteOn, 1, 50, TransportAction.NextSong)]
    public void SimulateInput_WithMatchingMapping_ShouldFireAction(
        MidiEventType type, int channel, int data1, TransportAction expectedAction)
    {
        var svc = CreateService();
        svc.Start("MockMIDI IN 1", BasicMappings);

        TransportAction? received = null;
        svc.TransportActionReceived += (_, a) => received = a;

        svc.SimulateInput(type, channel, data1);

        received.Should().Be(expectedAction);
    }

    [Fact]
    public void SimulateInput_WithNoMatchingMapping_ShouldNotFire()
    {
        var svc = CreateService();
        svc.Start("MockMIDI IN 1", BasicMappings);

        TransportAction? received = null;
        svc.TransportActionReceived += (_, a) => received = a;

        svc.SimulateInput(MidiEventType.ControlChange, 1, 99); // CC #99 non mappé

        received.Should().BeNull();
    }

    [Fact]
    public void SimulateInput_WhenStopped_ShouldNotFire()
    {
        var svc = CreateService();
        svc.Start("MockMIDI IN 1", BasicMappings);
        svc.Stop();

        TransportAction? received = null;
        svc.TransportActionReceived += (_, a) => received = a;

        svc.SimulateInput(MidiEventType.ControlChange, 1, 64);

        received.Should().BeNull();
    }

    // ------------------------------------------------------------------ //
    // Channel filtering
    // ------------------------------------------------------------------ //

    [Fact]
    public void SimulateInput_WithChannelFilter_ShouldMatchCorrectChannel()
    {
        var mappings = new List<MidiTransportMap>
        {
            new() { Action = TransportAction.Play, EventType = MidiEventType.ControlChange, Data1 = 64, Channel = 3 },
        };

        var svc = CreateService();
        svc.Start("MockMIDI IN 1", mappings);

        TransportAction? received = null;
        svc.TransportActionReceived += (_, a) => received = a;

        // Wrong channel → no fire
        svc.SimulateInput(MidiEventType.ControlChange, 1, 64);
        received.Should().BeNull();

        // Correct channel → fire
        svc.SimulateInput(MidiEventType.ControlChange, 3, 64);
        received.Should().Be(TransportAction.Play);
    }

    [Fact]
    public void SimulateInput_WithNullChannel_ShouldMatchAnyChannel()
    {
        var mappings = new List<MidiTransportMap>
        {
            new() { Action = TransportAction.Play, EventType = MidiEventType.ControlChange, Data1 = 64, Channel = null },
        };

        var svc = CreateService();
        svc.Start("MockMIDI IN 1", mappings);

        var received = new List<TransportAction>();
        svc.TransportActionReceived += (_, a) => received.Add(a);

        svc.SimulateInput(MidiEventType.ControlChange, 1, 64);
        svc.SimulateInput(MidiEventType.ControlChange, 7, 64);
        svc.SimulateInput(MidiEventType.ControlChange, 16, 64);

        received.Should().HaveCount(3).And.AllSatisfy(a => a.Should().Be(TransportAction.Play));
    }

    // ------------------------------------------------------------------ //
    // Unassigned mappings
    // ------------------------------------------------------------------ //

    [Fact]
    public void SimulateInput_WithUnassignedMapping_ShouldNotFire()
    {
        var mappings = new List<MidiTransportMap>
        {
            new() { Action = TransportAction.Play }, // IsAssigned = false
        };

        var svc = CreateService();
        svc.Start("MockMIDI IN 1", mappings);

        TransportAction? received = null;
        svc.TransportActionReceived += (_, a) => received = a;

        svc.SimulateInput(MidiEventType.ControlChange, 1, 0);

        received.Should().BeNull();
    }

    // ------------------------------------------------------------------ //
    // MIDI Learn
    // ------------------------------------------------------------------ //

    [Fact]
    public void MidiLearn_ShouldCaptureNextMessage()
    {
        var svc = CreateService();
        svc.Start("MockMIDI IN 1", BasicMappings);
        svc.StartLearn();

        MidiLearnResult? result = null;
        svc.MidiLearnReceived += (_, r) => result = r;

        svc.SimulateInput(MidiEventType.ControlChange, 2, 80);

        result.Should().NotBeNull();
        result!.EventType.Should().Be(MidiEventType.ControlChange);
        result.Channel.Should().Be(2);
        result.Data1.Should().Be(80);
    }

    [Fact]
    public void MidiLearn_ShouldDisableAfterCapture()
    {
        var svc = CreateService();
        svc.Start("MockMIDI IN 1", BasicMappings);
        svc.StartLearn();

        var learnResults = new List<MidiLearnResult>();
        svc.MidiLearnReceived += (_, r) => learnResults.Add(r);

        // Premier message : capturé en Learn
        svc.SimulateInput(MidiEventType.ControlChange, 1, 80);

        // Deuxième message : Learn désactivé, doit matcher le mapping normal
        var actions = new List<TransportAction>();
        svc.TransportActionReceived += (_, a) => actions.Add(a);
        svc.SimulateInput(MidiEventType.ControlChange, 1, 64); // CC #64 = Play

        learnResults.Should().HaveCount(1);
        actions.Should().ContainSingle().Which.Should().Be(TransportAction.Play);
    }

    [Fact]
    public void StopLearn_ShouldCancelWithoutCapture()
    {
        var svc = CreateService();
        svc.Start("MockMIDI IN 1", BasicMappings);
        svc.StartLearn();
        svc.StopLearn();

        MidiLearnResult? result = null;
        svc.MidiLearnReceived += (_, r) => result = r;

        var actions = new List<TransportAction>();
        svc.TransportActionReceived += (_, a) => actions.Add(a);

        svc.SimulateInput(MidiEventType.ControlChange, 1, 64); // CC #64 = Play

        result.Should().BeNull();
        actions.Should().ContainSingle().Which.Should().Be(TransportAction.Play);
    }

    // ------------------------------------------------------------------ //
    // Dispose
    // ------------------------------------------------------------------ //

    [Fact]
    public void Dispose_ShouldStopListening()
    {
        var svc = CreateService();
        svc.Start("MockMIDI IN 1", BasicMappings);

        TransportAction? received = null;
        svc.TransportActionReceived += (_, a) => received = a;

        svc.Dispose();
        svc.SimulateInput(MidiEventType.ControlChange, 1, 64);

        received.Should().BeNull();
    }
}
