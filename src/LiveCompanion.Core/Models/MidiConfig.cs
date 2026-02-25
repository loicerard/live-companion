namespace LiveCompanion.Core.Models;

/// <summary>
/// Configuration du moteur MIDI (ports sélectionnés).
/// </summary>
public class MidiConfig
{
    /// <summary>
    /// Ports MIDI OUT sélectionnés (maximum 6).
    /// </summary>
    public List<string> SelectedPorts { get; init; } = [];
}
