using LiveCompanion.Core.Models;
using LiveCompanion.Core.Validation;

namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Contrat de persistance du projet. Charge et sauvegarde les morceaux (JSON).
/// </summary>
public interface IProjectStore
{
    // ------------------------------------------------------------------ //
    // Songs
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Charge un morceau depuis un fichier JSON avec validation.
    /// </summary>
    /// <param name="path">Chemin du fichier projet.</param>
    /// <returns>Le résultat du chargement avec validation intégrée.</returns>
    Task<LoadResult<Song>> LoadAsync(string path);

    /// <summary>
    /// Sauvegarde un morceau dans un fichier JSON après validation.
    /// </summary>
    /// <param name="song">Morceau à sauvegarder.</param>
    /// <param name="path">Chemin du fichier de destination.</param>
    /// <returns>Le résultat de la validation. Si invalide, le fichier n'est pas écrit.</returns>
    Task<ValidationResult> SaveAsync(Song song, string path);

    /// <summary>
    /// Crée un nouveau morceau vide avec les valeurs par défaut.
    /// </summary>
    Song CreateNew(string title = "Nouveau morceau");

    /// <summary>
    /// Retourne tous les morceaux actuellement en mémoire.
    /// </summary>
    IReadOnlyList<Song> GetAll();

    /// <summary>
    /// Met à jour un morceau existant dans le store (met à jour <see cref="Song.LastModified"/>).
    /// </summary>
    /// <param name="song">Morceau modifié.</param>
    void Update(Song song);

    /// <summary>
    /// Supprime un morceau par son identifiant.
    /// </summary>
    /// <param name="songId">Identifiant du morceau à supprimer.</param>
    /// <returns><c>true</c> si le morceau existait et a été supprimé.</returns>
    bool Delete(Guid songId);

    // ------------------------------------------------------------------ //
    // Playlists
    // ------------------------------------------------------------------ //

    /// <summary>Crée une nouvelle playlist vide.</summary>
    Playlist CreatePlaylist(string name = "Playlist");

    /// <summary>Retourne toutes les playlists en mémoire.</summary>
    IReadOnlyList<Playlist> GetAllPlaylists();

    /// <summary>Met à jour une playlist existante.</summary>
    void UpdatePlaylist(Playlist playlist);

    /// <summary>Supprime une playlist par son identifiant.</summary>
    bool DeletePlaylist(Guid playlistId);

    /// <summary>Sauvegarde toutes les playlists dans un fichier JSON.</summary>
    Task<ValidationResult> SavePlaylistsAsync(string path);

    /// <summary>Charge les playlists depuis un fichier JSON avec validation de cohérence.</summary>
    Task<LoadResult<IReadOnlyList<Playlist>>> LoadPlaylistsAsync(string path);

    // ------------------------------------------------------------------ //
    // Settings
    // ------------------------------------------------------------------ //

    /// <summary>Retourne les paramètres globaux de l'application.</summary>
    AppSettings GetSettings();

    /// <summary>Sauvegarde les paramètres globaux.</summary>
    void SaveSettings(AppSettings settings);
}
