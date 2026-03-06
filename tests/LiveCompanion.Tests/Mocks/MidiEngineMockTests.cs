using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineMock;
using Xunit;

namespace LiveCompanion.Tests.Mocks;

public class MidiEngineMockTests
{
    private readonly ILogService _log = new DebugLogService();

    private MidiEngineMock CreateEngine() => new(_log);

    private static MidiConfig DefaultConfig => new()
    {
        SelectedPorts = { "MockMIDI Port 1" },
    };

    [Fact]
    public async Task Initialize_ShouldSucceed()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        engine.SentEvents.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailablePorts_ShouldReturnFakePorts()
    {
        var engine = CreateEngine();
        var ports = engine.GetAvailablePorts();

        ports.Should().HaveCount(3);
        ports.Should().Contain("MockMIDI Port 1");
    }

    [Fact]
    public async Task SendEvent_WithoutInit_ShouldThrow()
    {
        var engine = CreateEngine();
        var evt = new MidiEvent { Type = MidiEventType.NoteOn, Channel = 1, Data1 = 60 };

        var act = () => engine.SendEventAsync(evt);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendEvent_ShouldRecordEvent()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        var evt = new MidiEvent
        {
            Type = MidiEventType.ControlChange,
            Channel = 1,
            Data1 = 7,
            Data2 = 100,
            DeviceOut = "MockMIDI Port 1",
        };

        await engine.SendEventAsync(evt);

        engine.SentEvents.Should().ContainSingle();
        engine.SentEvents[0].Type.Should().Be(MidiEventType.ControlChange);
        engine.SentEvents[0].Data1.Should().Be(7);
    }

    [Fact]
    public async Task SendEvent_MultipleSends_ShouldAccumulate()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        await engine.SendEventAsync(new MidiEvent { Type = MidiEventType.NoteOn });
        await engine.SendEventAsync(new MidiEvent { Type = MidiEventType.NoteOff });
        await engine.SendEventAsync(new MidiEvent { Type = MidiEventType.ProgramChange });

        engine.SentEvents.Should().HaveCount(3);
    }

    [Fact]
    public async Task Shutdown_ShouldClearSentEvents()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);
        await engine.SendEventAsync(new MidiEvent { Type = MidiEventType.NoteOn });

        await engine.ShutdownAsync();

        engine.SentEvents.Should().BeEmpty();

        // After shutdown, send should throw
        var act = () => engine.SendEventAsync(new MidiEvent { Type = MidiEventType.NoteOn });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendEvent_NullEvent_ShouldThrow()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        var act = () => engine.SendEventAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Initialize_NullConfig_ShouldThrow()
    {
        var engine = CreateEngine();

        var act = () => engine.InitializeAsync(null!);
        act.Should().ThrowAsync<ArgumentNullException>();
    }
}
