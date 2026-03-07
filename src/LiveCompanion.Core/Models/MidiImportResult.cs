namespace LiveCompanion.Core.Models;

/// <summary>
/// Résultat de l'import d'un fichier MIDI : les sections extraites pour remplir un Song.
/// </summary>
public class MidiImportResult
{
    /// <summary>Titre déduit du fichier (nom de fichier sans extension).</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Sections extraites du fichier MIDI (décompte inclus en premier si demandé).</summary>
    public List<Section> Sections { get; init; } = [];
}
