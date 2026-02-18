namespace LiveCompanion.Midi.Tests;

/// <summary>
/// Tests for <see cref="MidiInputHandler"/> — verifies that incoming MIDI messages
/// are correctly mapped to player actions.
/// </summary>
public class MidiInputHandlerTests
{
    private static (MidiInputHandler Handler, FakeMidiInput Port, FakeMidiPortFactory Factory)
        BuildHandler(MidiConfiguration? config = null)
    {
        config ??= new MidiConfiguration
        {
            MidiInputPortName = "Roland SSPD Input",
            InputMappings =
            [
                // CC 64 (sustain pedal) value 127 on ch 0 → NextSong
                new MidiInputMapping { StatusType = 0xB0, Channel = 0, Data1 = 64, Data2 = 127, Action = MidiAction.NextSong },
                // CC 64 value 0 on ch 0 → PreviousSong
                new MidiInputMapping { StatusType = 0xB0, Channel = 0, Data1 = 64, Data2 = 0,   Action = MidiAction.PreviousSong },
                // CC 65 any value on ch 0 → Stop
                new MidiInputMapping { StatusType = 0xB0, Channel = 0, Data1 = 65, Data2 = -1,  Action = MidiAction.Stop },
                // CC 66 any value, any channel → TriggerCue
                new MidiInputMapping { StatusType = 0xB0, Channel = -1, Data1 = 66, Data2 = -1, Action = MidiAction.TriggerCue },
            ],
        };

        var factory = new FakeMidiPortFactory();
        var fakePort = factory.RegisterInput("Roland SSPD Input");
        var handler = new MidiInputHandler(factory, config);

        return (handler, fakePort, factory);
    }

    // ── Mapping accuracy ──────────────────────────────────────────

    [Fact]
    public void CC_64_value_127_triggers_NextSong()
    {
        var (handler, _, _) = BuildHandler();
        var actions = new List<MidiAction>();
        handler.ActionTriggered += a => actions.Add(a);
        handler.Open();

        handler.ProcessMessage(0xB0, 64, 127);

        Assert.Single(actions);
        Assert.Equal(MidiAction.NextSong, actions[0]);
    }

    [Fact]
    public void CC_64_value_0_triggers_PreviousSong()
    {
        var (handler, _, _) = BuildHandler();
        var actions = new List<MidiAction>();
        handler.ActionTriggered += a => actions.Add(a);
        handler.Open();

        handler.ProcessMessage(0xB0, 64, 0);

        Assert.Single(actions);
        Assert.Equal(MidiAction.PreviousSong, actions[0]);
    }

    [Fact]
    public void CC_65_any_value_triggers_Stop()
    {
        var (handler, _, _) = BuildHandler();
        var actions = new List<MidiAction>();
        handler.ActionTriggered += a => actions.Add(a);
        handler.Open();

        handler.ProcessMessage(0xB0, 65, 42);
        handler.ProcessMessage(0xB0, 65, 100);

        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.Equal(MidiAction.Stop, a));
    }

    [Fact]
    public void CC_66_any_channel_triggers_TriggerCue()
    {
        var (handler, _, _) = BuildHandler();
        var actions = new List<MidiAction>();
        handler.ActionTriggered += a => actions.Add(a);
        handler.Open();

        handler.ProcessMessage(0xB0, 66, 1); // ch 0
        handler.ProcessMessage(0xB5, 66, 1); // ch 5

        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.Equal(MidiAction.TriggerCue, a));
    }

    [Fact]
    public void Unmatched_message_fires_no_event()
    {
        var (handler, _, _) = BuildHandler();
        var actions = new List<MidiAction>();
        handler.ActionTriggered += a => actions.Add(a);
        handler.Open();

        handler.ProcessMessage(0xB0, 99, 127); // no mapping for CC 99

        Assert.Empty(actions);
    }

    [Fact]
    public void First_matching_rule_wins_when_multiple_could_match()
    {
        // Two rules: CC 70 on any channel → Stop, CC 70 on ch 0 → Pause
        // The Stop rule comes first and should win.
        var config = new MidiConfiguration
        {
            MidiInputPortName = "Test",
            InputMappings =
            [
                new MidiInputMapping { StatusType = 0xB0, Channel = -1, Data1 = 70, Data2 = -1, Action = MidiAction.Stop },
                new MidiInputMapping { StatusType = 0xB0, Channel = 0,  Data1 = 70, Data2 = -1, Action = MidiAction.Pause },
            ],
        };

        var factory = new FakeMidiPortFactory();
        factory.RegisterInput("Test");
        var handler = new MidiInputHandler(factory, config);

        var actions = new List<MidiAction>();
        handler.ActionTriggered += a => actions.Add(a);
        handler.Open();

        handler.ProcessMessage(0xB0, 70, 127);

        Assert.Single(actions);
        Assert.Equal(MidiAction.Stop, actions[0]);
    }

    // ── Port lifecycle ────────────────────────────────────────────

    [Fact]
    public void Open_starts_the_input_port()
    {
        var (handler, fakePort, _) = BuildHandler();

        handler.Open();

        Assert.True(fakePort.IsStarted);
    }

    [Fact]
    public void Close_stops_the_input_port()
    {
        var (handler, fakePort, _) = BuildHandler();
        handler.Open();

        handler.Close();

        Assert.False(fakePort.IsStarted);
    }

    [Fact]
    public void No_port_opened_when_no_input_port_configured()
    {
        var config = new MidiConfiguration { MidiInputPortName = null };
        var factory = new FakeMidiPortFactory();
        var handler = new MidiInputHandler(factory, config);

        handler.Open(); // should be a no-op

        Assert.Empty(factory.Inputs);
    }

    [Fact]
    public void Open_emits_MidiFault_when_port_not_available()
    {
        var config = new MidiConfiguration { MidiInputPortName = "NonExistent Port" };
        var factory = new FakeMidiPortFactory { ThrowOnOpen = true };
        var handler = new MidiInputHandler(factory, config);

        var faults = new List<string>();
        handler.MidiFault += (name, _) => faults.Add(name);

        handler.Open();

        Assert.Single(faults);
        Assert.Equal("NonExistent Port", faults[0]);
    }

    // ── Message parsing ───────────────────────────────────────────

    [Fact]
    public void ProcessMessage_handles_system_realtime_note_on()
    {
        var config = new MidiConfiguration
        {
            MidiInputPortName = "Test",
            InputMappings =
            [
                // Note On ch 0, note 60, any velocity → Pause
                new MidiInputMapping { StatusType = 0x90, Channel = 0, Data1 = 60, Data2 = -1, Action = MidiAction.Pause },
            ],
        };

        var factory = new FakeMidiPortFactory();
        factory.RegisterInput("Test");
        var handler = new MidiInputHandler(factory, config);

        var actions = new List<MidiAction>();
        handler.ActionTriggered += a => actions.Add(a);
        handler.Open();

        handler.ProcessMessage(0x90, 60, 100); // Note On ch 0, note 60, vel 100

        Assert.Single(actions);
        Assert.Equal(MidiAction.Pause, actions[0]);
    }
}
