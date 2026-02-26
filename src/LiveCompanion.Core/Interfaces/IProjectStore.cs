using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Contrat de persistance du projet. Charge et sauvegarde les morceaux (JSON).
/// </summary>
public interface IProjectStore
{
    /// <summary>
    /// Charge un morceau depuis un fichier JSON.
    /// </summary>
    /// <param name="path">Chemin du fichier projet.</param>
    /// <returns>Le morceau chargé, ou <c>null</c> si le fichier est introuvable.</returns>
    Task<Song?> LoadAsync(string path);

    /// <summary>
    /// Sauvegarde un morceau dans un fichier JSON.
    /// </summary>
    /// <param name="song">Morceau à sauvegarder.</param>
    /// <param name="path">Chemin du fichier de destination.</param>
    Task SaveAsync(Song song, string path);

    /// <summary>
    /// Crée un nouveau morceau vide avec les valeurs par défaut.
    /// </summary>
    Song CreateNew(string title = "Nouveau morceau");

    /// <summary>
    /// Retourne tous les morceaux actuellement en mémoire.
    /// </summary>
    IReadOnlyList<Song> GetAll();

    /// <summary>
    /// Supprime un morceau par son identifiant.
    /// </summary>
    /// <param name="songId">Identifiant du morceau à supprimer.</param>
    /// <returns><c>true</c> si le morceau existait et a été supprimé.</returns>
    bool Delete(Guid songId);
}
