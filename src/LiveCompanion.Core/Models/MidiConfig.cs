namespace LiveCompanion.Core.Models;

/// <summary>
/// Configuration du moteur MIDI (ports sélectionnés, entrée et mappings transport).
/// </summary>
public class MidiConfig
{
    /// <summary>
    /// Ports MIDI OUT sélectionnés (maximum 6).
    /// </summary>
    public List<string> SelectedPorts { get; init; } = [];

    /// <summary>
    /// Port MIDI IN utilisé pour le contrôle du transport (Play/Stop/Next/Previous).
    /// Null si aucun port d'entrée n'est sélectionné.
    /// </summary>
    public string? InputPort { get; init; }

    /// <summary>
    /// Mappings MIDI → actions de transport (Play, Stop, NextSection, PreviousSong, NextSong).
    /// </summary>
    public List<MidiTransportMap> TransportMappings { get; init; } = [];
}
