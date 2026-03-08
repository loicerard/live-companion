namespace LiveCompanion.Core.Models;

/// <summary>
/// Profil MIDI représentant un appareil (ex : "Quad Cortex") avec ses raccourcis prédéfinis.
/// </summary>
public class MidiProfile
{
    /// <summary>Identifiant unique du profil.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Nom du profil / appareil (ex : "Quad Cortex").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Raccourcis MIDI associés à cet appareil.</summary>
    public List<MidiPreset> Presets { get; set; } = [];
}
