namespace LiveCompanion.Core.Models;

/// <summary>
/// Associe une action de transport à un message MIDI entrant (CC ou Note).
/// Un mapping sans <see cref="EventType"/> est considéré comme non assigné.
/// </summary>
public class MidiTransportMap
{
    /// <summary>Action à déclencher lorsque le message MIDI correspondant est reçu.</summary>
    public TransportAction Action { get; set; }

    /// <summary>Type de message MIDI attendu (NoteOn, ControlChange…). Null = non assigné.</summary>
    public MidiEventType? EventType { get; set; }

    /// <summary>Canal MIDI (1–16). Null = n'importe quel canal.</summary>
    public int? Channel { get; set; }

    /// <summary>Numéro de CC ou de note (0–127). Null = non assigné.</summary>
    public int? Data1 { get; set; }

    /// <summary>Retourne true si le mapping est entièrement configuré.</summary>
    public bool IsAssigned => EventType.HasValue && Data1.HasValue;

    /// <summary>Texte affiché dans l'UI (ex : "CC #64 ch.1" ou "Note C3").</summary>
    public string DisplayText => IsAssigned
        ? $"{EventType} #{Data1}{(Channel.HasValue ? $" ch.{Channel}" : "")}"
        : "—";
}
