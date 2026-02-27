namespace LiveCompanion.Core.Models;

/// <summary>
/// Paramètres globaux de l'application (config audio, MIDI, préférences).
/// </summary>
public class AppSettings
{
    /// <summary>Configuration audio courante (driver, buffer, bus mappings).</summary>
    public AudioConfig? AudioConfig { get; set; }

    /// <summary>Configuration MIDI courante (ports sélectionnés).</summary>
    public MidiConfig? MidiConfig { get; set; }
}
