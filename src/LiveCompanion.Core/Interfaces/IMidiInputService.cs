using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Service d'écoute MIDI IN pour le contrôle du transport (Play, Stop, Next, Previous).
/// Supporte également un mode "MIDI Learn" pour assigner les mappings interactivement.
/// </summary>
public interface IMidiInputService : IDisposable
{
    /// <summary>
    /// Retourne la liste des noms de ports MIDI IN disponibles sur le système.
    /// </summary>
    IReadOnlyList<string> GetAvailableInputPorts();

    /// <summary>
    /// Démarre l'écoute sur le port spécifié avec les mappings transport donnés.
    /// Si un port était déjà ouvert, il est fermé avant d'ouvrir le nouveau.
    /// </summary>
    void Start(string portName, IReadOnlyList<MidiTransportMap> mappings);

    /// <summary>
    /// Arrête l'écoute et ferme le port MIDI IN.
    /// </summary>
    void Stop();

    /// <summary>
    /// Déclenché quand un message MIDI reçu correspond à un mapping de transport.
    /// </summary>
    event EventHandler<TransportAction>? TransportActionReceived;

    /// <summary>
    /// Active le mode MIDI Learn. Le prochain message MIDI reçu (CC ou NoteOn)
    /// est capturé et signalé via <see cref="MidiLearnReceived"/>.
    /// Le mode Learn se désactive automatiquement après réception d'un message.
    /// </summary>
    void StartLearn();

    /// <summary>
    /// Désactive le mode MIDI Learn sans attendre de message.
    /// </summary>
    void StopLearn();

    /// <summary>
    /// Déclenché quand un message MIDI est capturé en mode Learn.
    /// </summary>
    event EventHandler<MidiLearnResult>? MidiLearnReceived;
}

/// <summary>
/// Résultat d'une capture MIDI Learn : type, canal et data1 du message reçu.
/// </summary>
public record MidiLearnResult(MidiEventType EventType, int Channel, int Data1);
