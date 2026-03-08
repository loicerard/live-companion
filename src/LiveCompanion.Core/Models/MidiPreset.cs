namespace LiveCompanion.Core.Models;

/// <summary>
/// Preset MIDI réutilisable (ex : "QC Footswitch A" → ControlChange CC35 ch.1).
/// Ne contient ni Position ni DeviceOut, qui sont spécifiques au contexte d'utilisation.
/// </summary>
public class MidiPreset
{
    /// <summary>Identifiant unique du preset.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Nom affiché dans l'interface (ex : "QC Footswitch A").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type d'événement MIDI.</summary>
    public MidiEventType Type { get; set; } = MidiEventType.ControlChange;

    /// <summary>Canal MIDI (1–16).</summary>
    public int Channel { get; set; } = 1;

    /// <summary>Premier octet de données (numéro de CC, numéro de note…).</summary>
    public int Data1 { get; set; }

    /// <summary>Second octet de données (valeur CC, vélocité…).</summary>
    public int Data2 { get; set; }
}
