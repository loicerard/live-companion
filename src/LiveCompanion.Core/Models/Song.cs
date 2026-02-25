namespace LiveCompanion.Core.Models;

/// <summary>
/// Représente un morceau complet avec ses sections, ses samples et ses événements MIDI.
/// C'est l'entité racine du domaine.
/// </summary>
public class Song
{
    /// <summary>Identifiant unique du morceau.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Titre du morceau.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Sections du morceau, dans l'ordre d'exécution.</summary>
    public List<Section> Sections { get; init; } = [];

    /// <summary>Samples audio associés au morceau.</summary>
    public List<AudioClip> AudioClips { get; init; } = [];

    /// <summary>Événements MIDI programmés dans le morceau.</summary>
    public List<MidiEvent> MidiEvents { get; init; } = [];

    /// <summary>Chemin du fichier de piste de clic (optionnel).</summary>
    public string? ClickTrackPath { get; set; }

    /// <summary>Date de dernière modification.</summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
