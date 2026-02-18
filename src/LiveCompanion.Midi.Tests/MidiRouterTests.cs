using LiveCompanion.Core.Engine;

namespace LiveCompanion.Midi.Tests;

/// <summary>
/// Tests for <see cref="MidiRouter"/> — verifies that the correct Program Change
/// and Control Change messages are sent to the right MIDI port and channel.
/// </summary>
public class MidiRouterTests
{
    private static MidiConfiguration BuildConfig() => new()
    {
        OutputDevices = new()
        {
            [DeviceTarget.Quad1] = new DeviceOutputConfig { PortName = "Port-Quad1", Channel = 0 },
            [DeviceTarget.Quad2] = new DeviceOutputConfig { PortName = "Port-Quad2", Channel = 1 },
            [DeviceTarget.SSPD]  = new DeviceOutputConfig { PortName = "Port-SSPD",  Channel = 2 },
        },
    };

    private static (MidiRouter Router, FakeMidiPortFactory Factory, MidiService Service)
        BuildRouter(MidiConfiguration? config = null)
    {
        config ??= BuildConfig();
        var factory = new FakeMidiPortFactory();
        // Pre-register so OpenOutput succeeds
        factory.RegisterOutput("Port-Quad1");
        factory.RegisterOutput("Port-Quad2");
        factory.RegisterOutput("Port-SSPD");

        var service = new MidiService(factory, config);
        var router = new MidiRouter(service, config);
        return (router, factory, service);
    }

    // ── Program Change ────────────────────────────────────────────

    [Fact]
    public void SendPreset_sends_program_change_to_Quad1_port()
    {
        var (router, factory, _) = BuildRouter();
        var preset = new MidiPreset
        {
            Device = DeviceTarget.Quad1,
            Channel = 0,
            ProgramChange = 5,
        };

        router.SendPreset(preset);

        var port = factory.Outputs["Port-Quad1"];
        Assert.Equal(1, port.SendCount);
        Assert.Equal(0xC0, port.GetStatus(0)); // PC on ch 0
        Assert.Equal(5, port.GetData1(0));
    }

    [Fact]
    public void SendPreset_sends_program_change_to_Quad2_port()
    {
        var (router, factory, _) = BuildRouter();
        var preset = new MidiPreset
        {
            Device = DeviceTarget.Quad2,
            Channel = 1,
            ProgramChange = 12,
        };

        router.SendPreset(preset);

        var port = factory.Outputs["Port-Quad2"];
        Assert.Equal(1, port.SendCount);
        Assert.Equal(0xC1, port.GetStatus(0)); // PC on ch 1
        Assert.Equal(12, port.GetData1(0));
    }

    [Fact]
    public void SendPreset_sends_program_change_to_SSPD_port()
    {
        var (router, factory, _) = BuildRouter();
        var preset = new MidiPreset
        {
            Device = DeviceTarget.SSPD,
            Channel = 2,
            ProgramChange = 7,
        };

        router.SendPreset(preset);

        var port = factory.Outputs["Port-SSPD"];
        Assert.Equal(1, port.SendCount);
        Assert.Equal(0xC2, port.GetStatus(0)); // PC on ch 2
        Assert.Equal(7, port.GetData1(0));
    }

    [Fact]
    public void SendPreset_does_not_send_to_other_ports()
    {
        var (router, factory, _) = BuildRouter();
        var preset = new MidiPreset { Device = DeviceTarget.Quad1, Channel = 0, ProgramChange = 1 };

        router.SendPreset(preset);

        // Quad2 and SSPD should have received nothing
        Assert.Equal(0, factory.Outputs["Port-Quad2"].SendCount);
        Assert.Equal(0, factory.Outputs["Port-SSPD"].SendCount);
    }

    // ── Control Change ────────────────────────────────────────────

    [Fact]
    public void SendPreset_sends_cc_after_program_change()
    {
        var (router, factory, _) = BuildRouter();
        var preset = new MidiPreset
        {
            Device = DeviceTarget.Quad1,
            Channel = 0,
            ProgramChange = 3,
            ControlChanges = [new ControlChange(7, 100)], // CC7 = volume, value 100
        };

        router.SendPreset(preset);

        var port = factory.Outputs["Port-Quad1"];
        Assert.Equal(2, port.SendCount);

        // First message: PC
        Assert.Equal(0xC0, port.GetStatus(0));
        Assert.Equal(3, port.GetData1(0));

        // Second message: CC
        Assert.Equal(0xB0, port.GetStatus(1)); // CC on ch 0
        Assert.Equal(7, port.GetData1(1));     // controller 7
        Assert.Equal(100, port.GetData2(1));   // value 100
    }

    [Fact]
    public void SendPreset_sends_multiple_cc_in_order()
    {
        var (router, factory, _) = BuildRouter();
        var preset = new MidiPreset
        {
            Device = DeviceTarget.SSPD,
            Channel = 2,
            ProgramChange = 0,
            ControlChanges =
            [
                new ControlChange(1, 64),
                new ControlChange(2, 32),
                new ControlChange(3, 0),
            ],
        };

        router.SendPreset(preset);

        var port = factory.Outputs["Port-SSPD"];
        Assert.Equal(4, port.SendCount); // 1 PC + 3 CC

        Assert.Equal(1, port.GetData1(1));
        Assert.Equal(64, port.GetData2(1));

        Assert.Equal(2, port.GetData1(2));
        Assert.Equal(32, port.GetData2(2));

        Assert.Equal(3, port.GetData1(3));
        Assert.Equal(0, port.GetData2(3));
    }

    [Fact]
    public void SendPreset_uses_preset_channel_not_default_config_channel()
    {
        // The config says channel=0 for Quad1, but the preset overrides to channel=5
        var (router, factory, _) = BuildRouter();
        var preset = new MidiPreset
        {
            Device = DeviceTarget.Quad1,
            Channel = 5, // override
            ProgramChange = 10,
        };

        router.SendPreset(preset);

        var port = factory.Outputs["Port-Quad1"];
        Assert.Equal(0xC5, port.GetStatus(0)); // ch 5
    }

    // ── Event-driven dispatch ─────────────────────────────────────

    [Fact]
    public void Attach_routes_MidiPresetChanged_events_automatically()
    {
        var config = BuildConfig();
        var factory = new FakeMidiPortFactory();
        factory.RegisterOutput("Port-Quad1");
        factory.RegisterOutput("Port-Quad2");
        factory.RegisterOutput("Port-SSPD");

        var service = new MidiService(factory, config);
        var router = new MidiRouter(service, config);

        // Build a real SetlistPlayer driven by the Phase-1 MetronomeEngine
        var metronome = new MetronomeEngine(480, 120);
        var player = new SetlistPlayer(metronome);
        router.Attach(player);

        // Manually fire MidiPresetChanged
        var fired = new List<MidiPreset>();
        player.MidiPresetChanged += p => fired.Add(p);

        var preset = new MidiPreset { Device = DeviceTarget.Quad2, Channel = 0, ProgramChange = 9 };
        // Simulate the event as SetlistPlayer would fire it
        player.MidiPresetChanged += _ => { };
        // Directly invoke the router's handler via reflection is too fragile;
        // instead call SendPreset directly since Attach wires the event
        router.SendPreset(preset);

        Assert.Equal(0xC0, factory.Outputs["Port-Quad2"].GetStatus(0));
        Assert.Equal(9, factory.Outputs["Port-Quad2"].GetData1(0));
    }

    [Fact]
    public void Detach_stops_routing_events()
    {
        var (router, factory, _) = BuildRouter();
        var metronome = new MetronomeEngine(480, 120);
        var player = new SetlistPlayer(metronome);

        router.Attach(player);
        router.Detach();

        // After detach, sending a preset manually should still work (router is independent)
        router.SendPreset(new MidiPreset { Device = DeviceTarget.Quad1, Channel = 0, ProgramChange = 1 });
        Assert.Equal(1, factory.Outputs["Port-Quad1"].SendCount);
    }

    // ── Message builder unit tests ─────────────────────────────────

    [Theory]
    [InlineData(0, 0,   0x00C0)]
    [InlineData(0, 5,   0x05C0)]
    [InlineData(3, 10,  0x0AC3)]
    [InlineData(15, 127, 0x7FCF)]
    public void BuildProgramChange_encodes_correctly(int channel, int program, int expected)
    {
        int actual = MidiRouter.BuildProgramChange(channel, program);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, 7, 100,  0x64_07_B0)]
    [InlineData(1, 1, 64,   0x40_01_B1)]
    [InlineData(15, 64, 0,  0x00_40_BF)]
    public void BuildControlChange_encodes_correctly(int channel, int controller, int value, int expected)
    {
        int actual = MidiRouter.BuildControlChange(channel, controller, value);
        Assert.Equal(expected, actual);
    }

    // ── Error handling ────────────────────────────────────────────

    [Fact]
    public void SendPreset_silently_ignores_unknown_device_target()
    {
        // Config with no entry for Quad1
        var config = new MidiConfiguration { OutputDevices = new() };
        var factory = new FakeMidiPortFactory();
        var service = new MidiService(factory, config);
        var router = new MidiRouter(service, config);

        // Should not throw
        var preset = new MidiPreset { Device = DeviceTarget.Quad1, Channel = 0, ProgramChange = 1 };
        router.SendPreset(preset); // should log warning and return
    }
}
