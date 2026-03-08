namespace LiveCompanion.Core.Models;

/// <summary>
/// Actions de transport déclenchables via un événement MIDI entrant.
/// </summary>
public enum TransportAction
{
    Play,
    Stop,
    NextSection,
    PreviousSong,
    NextSong,
}
