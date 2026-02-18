using System.Text.Json;

namespace LiveCompanion.Midi.Tests;

/// <summary>
/// Tests for <see cref="MidiConfiguration"/> serialization and deserialization.
/// </summary>
public class MidiConfigurationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    [Fact]
    public void Default_configuration_has_three_output_devices()
    {
        var config = new MidiConfiguration();

        Assert.Contains(DeviceTarget.Quad1, config.OutputDevices.Keys);
        Assert.Contains(DeviceTarget.Quad2, config.OutputDevices.Keys);
        Assert.Contains(DeviceTarget.SSPD, config.OutputDevices.Keys);
    }

    [Fact]
    public void Default_clock_targets_include_both_quad_cortex()
    {
        var config = new MidiConfiguration();

        Assert.Contains(DeviceTarget.Quad1, config.ClockTargets);
        Assert.Contains(DeviceTarget.Quad2, config.ClockTargets);
        Assert.DoesNotContain(DeviceTarget.SSPD, config.ClockTargets);
    }

    [Fact]
    public void Serialization_roundtrip_preserves_output_devices()
    {
        var config = new MidiConfiguration();
        config.OutputDevices[DeviceTarget.Quad1].PortName = "My Quad 1";
        config.OutputDevices[DeviceTarget.Quad1].Channel = 3;

        string json = JsonSerializer.Serialize(config, Options);
        var restored = JsonSerializer.Deserialize<MidiConfiguration>(json, Options)!;

        Assert.Equal("My Quad 1", restored.OutputDevices[DeviceTarget.Quad1].PortName);
        Assert.Equal(3, restored.OutputDevices[DeviceTarget.Quad1].Channel);
    }

    [Fact]
    public void Serialization_roundtrip_preserves_input_mappings()
    {
        var config = new MidiConfiguration
        {
            MidiInputPortName = "Roland SSPD",
            InputMappings =
            [
                new MidiInputMapping
                {
                    StatusType = 0xB0,
                    Channel = 0,
                    Data1 = 64,
                    Data2 = 127,
                    Action = MidiAction.NextSong,
                },
            ],
        };

        string json = JsonSerializer.Serialize(config, Options);
        var restored = JsonSerializer.Deserialize<MidiConfiguration>(json, Options)!;

        Assert.Equal("Roland SSPD", restored.MidiInputPortName);
        Assert.Single(restored.InputMappings);
        Assert.Equal(MidiAction.NextSong, restored.InputMappings[0].Action);
        Assert.Equal(0xB0, restored.InputMappings[0].StatusType);
        Assert.Equal(64, restored.InputMappings[0].Data1);
        Assert.Equal(127, restored.InputMappings[0].Data2);
    }

    [Fact]
    public void Serialization_roundtrip_preserves_clock_targets()
    {
        var config = new MidiConfiguration();
        config.ClockTargets.Add(DeviceTarget.SSPD);

        string json = JsonSerializer.Serialize(config, Options);
        var restored = JsonSerializer.Deserialize<MidiConfiguration>(json, Options)!;

        Assert.Contains(DeviceTarget.Quad1, restored.ClockTargets);
        Assert.Contains(DeviceTarget.Quad2, restored.ClockTargets);
        Assert.Contains(DeviceTarget.SSPD, restored.ClockTargets);
    }

    [Fact]
    public void Serialization_roundtrip_preserves_reconnect_delay()
    {
        var config = new MidiConfiguration { ReconnectDelayMs = 3000 };

        string json = JsonSerializer.Serialize(config, Options);
        var restored = JsonSerializer.Deserialize<MidiConfiguration>(json, Options)!;

        Assert.Equal(3000, restored.ReconnectDelayMs);
    }

    [Fact]
    public void MidiInputMapping_Matches_cc_exact()
    {
        var mapping = new MidiInputMapping
        {
            StatusType = 0xB0,
            Channel = 0,
            Data1 = 64,
            Data2 = 127,
            Action = MidiAction.NextSong,
        };

        Assert.True(mapping.Matches(0xB0, 64, 127));   // exact match
        Assert.False(mapping.Matches(0xB1, 64, 127));  // wrong channel
        Assert.False(mapping.Matches(0xB0, 65, 127));  // wrong controller
        Assert.False(mapping.Matches(0xB0, 64, 0));    // wrong value
    }

    [Fact]
    public void MidiInputMapping_Matches_with_wildcards()
    {
        // Data1=-1 and Data2=-1 means "any value"
        var mapping = new MidiInputMapping
        {
            StatusType = 0xB0,
            Channel = -1, // any channel
            Data1 = 64,
            Data2 = -1,   // any value
            Action = MidiAction.Stop,
        };

        Assert.True(mapping.Matches(0xB0, 64, 0));
        Assert.True(mapping.Matches(0xB0, 64, 127));
        Assert.True(mapping.Matches(0xB5, 64, 42));    // any channel → ch 5 ok
        Assert.False(mapping.Matches(0xB0, 65, 0));    // wrong controller
    }

    [Fact]
    public void MidiAction_enum_serializes_as_string()
    {
        var mapping = new MidiInputMapping { Action = MidiAction.PreviousSong };

        string json = JsonSerializer.Serialize(mapping, Options);

        Assert.Contains("PreviousSong", json);
        Assert.DoesNotContain("3", json); // should not be numeric
    }
}
