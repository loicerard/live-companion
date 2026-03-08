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

    private static MidiProfile TestProfile => new()
    {
        Name = "Test Device",
        DeviceOut = "MockMIDI Port 1",
        DefaultChannel = 1,
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
        var profile = TestProfile;
        var evt = new MidiEvent { Type = MidiEventType.NoteOn, ProfileIds = [profile.Id], Data1 = 60 };

        var act = () => engine.SendEventAsync(evt, [profile]);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendEvent_ShouldRecordEvent()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        var profile = TestProfile;
        var evt = new MidiEvent
        {
            Type = MidiEventType.ControlChange,
            ProfileIds = [profile.Id],
            Data1 = 7,
            Data2 = 100,
        };

        await engine.SendEventAsync(evt, [profile]);

        engine.SentEvents.Should().ContainSingle();
        engine.SentEvents[0].Type.Should().Be(MidiEventType.ControlChange);
        engine.SentEvents[0].Data1.Should().Be(7);
        engine.SentEvents[0].DeviceOut.Should().Be("MockMIDI Port 1");
        engine.SentEvents[0].Channel.Should().Be(1);
    }

    [Fact]
    public async Task SendDirect_ShouldRecordEvent()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        await engine.SendDirectAsync(MidiEventType.NoteOn, "MockMIDI Port 1", 5, 60, 100);

        engine.SentEvents.Should().ContainSingle();
        engine.SentEvents[0].Type.Should().Be(MidiEventType.NoteOn);
        engine.SentEvents[0].DeviceOut.Should().Be("MockMIDI Port 1");
        engine.SentEvents[0].Channel.Should().Be(5);
        engine.SentEvents[0].Data1.Should().Be(60);
    }

    [Fact]
    public async Task SendEvent_MultipleProfiles_ShouldSendToEach()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        var profile1 = new MidiProfile { Name = "Device A", DeviceOut = "MockMIDI Port 1", DefaultChannel = 1 };
        var profile2 = new MidiProfile { Name = "Device B", DeviceOut = "MockMIDI Port 2", DefaultChannel = 10 };

        var evt = new MidiEvent
        {
            Type = MidiEventType.ControlChange,
            ProfileIds = [profile1.Id, profile2.Id],
            Data1 = 7,
            Data2 = 127,
        };

        await engine.SendEventAsync(evt, [profile1, profile2]);

        engine.SentEvents.Should().HaveCount(2);
        engine.SentEvents[0].DeviceOut.Should().Be("MockMIDI Port 1");
        engine.SentEvents[0].Channel.Should().Be(1);
        engine.SentEvents[1].DeviceOut.Should().Be("MockMIDI Port 2");
        engine.SentEvents[1].Channel.Should().Be(10);
    }

    [Fact]
    public async Task Shutdown_ShouldClearSentEvents()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);
        await engine.SendDirectAsync(MidiEventType.NoteOn, "MockMIDI Port 1", 1, 60, 100);

        await engine.ShutdownAsync();

        engine.SentEvents.Should().BeEmpty();

        // After shutdown, send should throw
        var act = () => engine.SendDirectAsync(MidiEventType.NoteOn, "MockMIDI Port 1", 1, 60, 100);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendEvent_NullEvent_ShouldThrow()
    {
        var engine = CreateEngine();
        await engine.InitializeAsync(DefaultConfig);

        var act = () => engine.SendEventAsync(null!, []);
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
