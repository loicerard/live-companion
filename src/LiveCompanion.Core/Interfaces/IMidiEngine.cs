using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Contrat du moteur MIDI. Gère la détection des ports de sortie et l'envoi d'événements.
/// </summary>
public interface IMidiEngine
{
    /// <summary>
    /// Initialise le moteur MIDI avec la configuration fournie (ports sélectionnés).
    /// </summary>
    /// <param name="config">Configuration MIDI à appliquer.</param>
    Task InitializeAsync(MidiConfig config);

    /// <summary>
    /// Retourne la liste des noms de ports MIDI OUT disponibles sur le système.
    /// </summary>
    IReadOnlyList<string> GetAvailablePorts();

    /// <summary>
    /// Envoie un événement MIDI immédiatement sur le device spécifié dans l'événement.
    /// </summary>
    /// <param name="midiEvent">Événement MIDI à envoyer.</param>
    Task SendEventAsync(MidiEvent midiEvent);

    /// <summary>Libère les ressources du moteur MIDI.</summary>
    Task ShutdownAsync();
}
