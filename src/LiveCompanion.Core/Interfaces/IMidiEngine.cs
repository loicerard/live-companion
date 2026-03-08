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
    /// Envoie un événement MIDI en résolvant les ports/canaux depuis les profils référencés.
    /// Le message est envoyé une fois par profil ciblé.
    /// </summary>
    Task SendEventAsync(MidiEvent midiEvent, IReadOnlyList<MidiProfile> profiles);

    /// <summary>
    /// Envoie un message MIDI directement sur un port et canal spécifiques.
    /// Utilisé pour les tests de configuration (sans passer par les profils).
    /// </summary>
    Task SendDirectAsync(MidiEventType type, string deviceOut, int channel, int data1, int data2);

    /// <summary>Libère les ressources du moteur MIDI.</summary>
    Task ShutdownAsync();
}
