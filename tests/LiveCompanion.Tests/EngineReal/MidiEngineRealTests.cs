using FluentAssertions;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineReal;

namespace LiveCompanion.Tests.EngineReal;

public class MidiEngineRealTests : IDisposable
{
    private readonly ILogService _log = new DebugLogService();
    private readonly MidiEngineReal _engine;

    public MidiEngineRealTests()
    {
        _engine = new MidiEngineReal(_log);
    }

    public void Dispose() => _engine.Dispose();

    // ------------------------------------------------------------------ //
    // Constructor
    // ------------------------------------------------------------------ //

    [Fact]
    public void Constructor_NullLog_ShouldThrow()
    {
        var act = () => new MidiEngineReal(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------ //
    // GetAvailablePorts
    // ------------------------------------------------------------------ //

    [Fact]
    public void GetAvailablePorts_ShouldReturnList()
    {
        // On CI there are likely no MIDI devices, but it should not throw
        var ports = _engine.GetAvailablePorts();
        ports.Should().NotBeNull();
    }

    // ------------------------------------------------------------------ //
    // Initialize
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Initialize_NullConfig_ShouldThrow()
    {
        var act = () => _engine.InitializeAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Initialize_EmptyPorts_ShouldSucceed()
    {
        var config = new MidiConfig { SelectedPorts = [] };
        await _engine.InitializeAsync(config);
        // No ports opened, but engine is initialized — no exception
    }

    [Fact]
    public async Task Initialize_UnknownPort_ShouldNotThrow()
    {
        var config = new MidiConfig { SelectedPorts = { "NonExistentPort_12345" } };
        await _engine.InitializeAsync(config);
        // Should log a warning but not throw
    }

    [Fact]
    public async Task Initialize_Twice_ShouldCloseAndReopenPorts()
    {
        var config = new MidiConfig { SelectedPorts = [] };
        await _engine.InitializeAsync(config);
        await _engine.InitializeAsync(config);
        // Should not throw — re-initialization is safe
    }

    // ------------------------------------------------------------------ //
    // SendEvent — without init
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task SendEvent_WithoutInit_ShouldThrow()
    {
        var evt = new MidiEvent { Type = MidiEventType.NoteOn, Channel = 1, Data1 = 60 };

        var act = () => _engine.SendEventAsync(evt);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendEvent_NullEvent_ShouldThrow()
    {
        await _engine.InitializeAsync(new MidiConfig { SelectedPorts = [] });

        var act = () => _engine.SendEventAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendEvent_UnknownDevice_ShouldNotThrow()
    {
        await _engine.InitializeAsync(new MidiConfig { SelectedPorts = [] });

        var evt = new MidiEvent
        {
            Type = MidiEventType.NoteOn,
            Channel = 1,
            Data1 = 60,
            Data2 = 100,
            DeviceOut = "UnknownDevice",
        };

        // Should warn but not throw
        await _engine.SendEventAsync(evt);
    }

    // ------------------------------------------------------------------ //
    // Shutdown
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task Shutdown_ShouldPreventFurtherSends()
    {
        await _engine.InitializeAsync(new MidiConfig { SelectedPorts = [] });
        await _engine.ShutdownAsync();

        var evt = new MidiEvent { Type = MidiEventType.NoteOn };
        var act = () => _engine.SendEventAsync(evt);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Shutdown_WithoutInit_ShouldNotThrow()
    {
        await _engine.ShutdownAsync();
    }

    // ------------------------------------------------------------------ //
    // BuildMidiMessage (internal, tested directly)
    // ------------------------------------------------------------------ //

    [Fact]
    public void BuildMidiMessage_NoteOn_ShouldBuildCorrectly()
    {
        var evt = new MidiEvent
        {
            Type = MidiEventType.NoteOn,
            Channel = 1,
            Data1 = 60,  // middle C
            Data2 = 100, // velocity
        };

        int msg = MidiEngineReal.BuildMidiMessage(evt);

        // Status = 0x90 (NoteOn ch1), Data1 = 60, Data2 = 100
        // Format: status | (data1 << 8) | (data2 << 16)
        int expected = 0x90 | (60 << 8) | (100 << 16);
        msg.Should().Be(expected);
    }

    [Fact]
    public void BuildMidiMessage_NoteOff_Channel10_ShouldBuildCorrectly()
    {
        var evt = new MidiEvent
        {
            Type = MidiEventType.NoteOff,
            Channel = 10,
            Data1 = 36,
            Data2 = 0,
        };

        int msg = MidiEngineReal.BuildMidiMessage(evt);

        // Status = 0x80 | 9 (channel 10 = index 9)
        int expected = (0x80 | 9) | (36 << 8) | (0 << 16);
        msg.Should().Be(expected);
    }

    [Fact]
    public void BuildMidiMessage_ControlChange_ShouldBuildCorrectly()
    {
        var evt = new MidiEvent
        {
            Type = MidiEventType.ControlChange,
            Channel = 1,
            Data1 = 7,   // volume
            Data2 = 127, // max
        };

        int msg = MidiEngineReal.BuildMidiMessage(evt);

        int expected = 0xB0 | (7 << 8) | (127 << 16);
        msg.Should().Be(expected);
    }

    [Fact]
    public void BuildMidiMessage_ProgramChange_ShouldHaveOnlyOneDataByte()
    {
        var evt = new MidiEvent
        {
            Type = MidiEventType.ProgramChange,
            Channel = 3,
            Data1 = 42,
            Data2 = 99, // should be ignored
        };

        int msg = MidiEngineReal.BuildMidiMessage(evt);

        // ProgramChange: status | (data1 << 8) — no data2
        int expected = (0xC0 | 2) | (42 << 8);
        msg.Should().Be(expected);
    }

    [Fact]
    public void BuildMidiMessage_ShouldClampChannel()
    {
        // Channel 0 → clamped to 1 → index 0
        var evtLow = new MidiEvent { Type = MidiEventType.NoteOn, Channel = 0, Data1 = 60, Data2 = 100 };
        int msgLow = MidiEngineReal.BuildMidiMessage(evtLow);
        (msgLow & 0x0F).Should().Be(0, "channel 0 should be clamped to 1 (index 0)");

        // Channel 17 → clamped to 16 → index 15
        var evtHigh = new MidiEvent { Type = MidiEventType.NoteOn, Channel = 17, Data1 = 60, Data2 = 100 };
        int msgHigh = MidiEngineReal.BuildMidiMessage(evtHigh);
        (msgHigh & 0x0F).Should().Be(15, "channel 17 should be clamped to 16 (index 15)");
    }

    [Fact]
    public void BuildMidiMessage_ShouldClampDataValues()
    {
        var evt = new MidiEvent
        {
            Type = MidiEventType.NoteOn,
            Channel = 1,
            Data1 = 200,  // > 127, should clamp
            Data2 = -10,  // < 0, should clamp
        };

        int msg = MidiEngineReal.BuildMidiMessage(evt);

        int data1 = (msg >> 8) & 0x7F;
        int data2 = (msg >> 16) & 0x7F;
        data1.Should().Be(127);
        data2.Should().Be(0);
    }

    [Fact]
    public void BuildMidiMessage_AllChannels_ShouldMapCorrectly()
    {
        for (int ch = 1; ch <= 16; ch++)
        {
            var evt = new MidiEvent { Type = MidiEventType.NoteOn, Channel = ch };
            int msg = MidiEngineReal.BuildMidiMessage(evt);
            (msg & 0x0F).Should().Be(ch - 1, $"channel {ch} should map to index {ch - 1}");
        }
    }
}
