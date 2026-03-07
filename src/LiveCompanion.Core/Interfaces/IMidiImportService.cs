using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Service d'import de fichier MIDI pour extraire la structure d'un morceau
/// (sections, tempos, signatures rythmiques).
/// </summary>
public interface IMidiImportService
{
    /// <summary>
    /// Importe un fichier MIDI et en extrait les sections du morceau.
    /// </summary>
    /// <param name="midiFilePath">Chemin du fichier MIDI.</param>
    /// <param name="countdownBars">Nombre de mesures de décompte à ajouter en début.</param>
    /// <returns>Le résultat de l'import contenant les sections extraites.</returns>
    MidiImportResult Import(string midiFilePath, int countdownBars = 3);
}
