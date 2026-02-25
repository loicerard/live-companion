namespace LiveCompanion.Core.Models;

/// <summary>
/// Événement MIDI programmé à une position précise dans la timeline.
/// </summary>
public class MidiEvent
{
    /// <summary>Identifiant unique de l'événement.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Type d'événement MIDI.</summary>
    public MidiEventType Type { get; set; } = MidiEventType.ProgramChange;

    /// <summary>Nom du port/device MIDI de sortie cible.</summary>
    public string DeviceOut { get; set; } = string.Empty;

    /// <summary>Canal MIDI (1–16).</summary>
    public int Channel { get; set; } = 1;

    /// <summary>
    /// Premier octet de données (numéro de programme, numéro de CC, numéro de note…).
    /// </summary>
    public int Data1 { get; set; }

    /// <summary>
    /// Second octet de données (valeur CC, vélocité…). Non utilisé pour Program Change.
    /// </summary>
    public int Data2 { get; set; }

    /// <summary>Position de déclenchement dans la timeline.</summary>
    public TimelinePosition Position { get; set; } = TimelinePosition.Zero;
}
