namespace LiveCompanion.Core.Models;

/// <summary>
/// État du transport audio/MIDI.
/// </summary>
public enum TransportState
{
    /// <summary>Arrêté. Aucune lecture en cours.</summary>
    Stopped,

    /// <summary>Lecture en cours.</summary>
    Playing,

    /// <summary>En pause. La position est conservée.</summary>
    Paused,
}
