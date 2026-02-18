using LiveCompanion.Midi.Abstractions;

namespace LiveCompanion.Midi.Tests.Fakes;

/// <summary>
/// Fake MIDI port factory for testing. Returns pre-configured fake ports.
/// </summary>
public sealed class FakeMidiPortFactory : IMidiPortFactory
{
    private readonly Dictionary<string, FakeMidiOutput> _outputs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FakeMidiInput> _inputs =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the next OpenOutput call should throw (simulates unavailable port).</summary>
    public bool ThrowOnOpen { get; set; }

    /// <summary>All output ports that have been registered or opened.</summary>
    public IReadOnlyDictionary<string, FakeMidiOutput> Outputs => _outputs;

    /// <summary>All input ports that have been registered or opened.</summary>
    public IReadOnlyDictionary<string, FakeMidiInput> Inputs => _inputs;

    /// <summary>Pre-registers an output port so it can be opened by name.</summary>
    public FakeMidiOutput RegisterOutput(string portName)
    {
        var port = new FakeMidiOutput(portName);
        _outputs[portName] = port;
        return port;
    }

    /// <summary>Pre-registers an input port so it can be opened by name.</summary>
    public FakeMidiInput RegisterInput(string portName)
    {
        var port = new FakeMidiInput(portName);
        _inputs[portName] = port;
        return port;
    }

    public string[] GetOutputPortNames() => [.. _outputs.Keys];
    public string[] GetInputPortNames() => [.. _inputs.Keys];

    public IMidiOutput OpenOutput(string portName)
    {
        if (ThrowOnOpen)
            throw new InvalidOperationException($"Simulated failure opening port '{portName}'.");

        if (_outputs.TryGetValue(portName, out var port))
            return port;

        // Auto-create if not pre-registered
        var newPort = new FakeMidiOutput(portName);
        _outputs[portName] = newPort;
        return newPort;
    }

    public IMidiInput OpenInput(string portName)
    {
        if (ThrowOnOpen)
            throw new InvalidOperationException($"Simulated failure opening port '{portName}'.");

        if (_inputs.TryGetValue(portName, out var port))
            return port;

        var newPort = new FakeMidiInput(portName);
        _inputs[portName] = newPort;
        return newPort;
    }
}
