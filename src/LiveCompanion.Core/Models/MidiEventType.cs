namespace LiveCompanion.Core.Models;

/// <summary>
/// Type d'événement MIDI supporté par le moteur.
/// </summary>
public enum MidiEventType
{
    /// <summary>Program Change : change le programme/preset d'un device.</summary>
    ProgramChange,

    /// <summary>Control Change : envoie une valeur de contrôle (ex : volume, expression).</summary>
    ControlChange,

    /// <summary>Note On : déclenche une note.</summary>
    NoteOn,

    /// <summary>Note Off : relâche une note.</summary>
    NoteOff,
}
