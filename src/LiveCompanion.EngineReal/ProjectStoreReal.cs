using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Implémentation réelle de la persistance projet (JSON sur disque).
/// Stub : toutes les méthodes lèvent <see cref="NotImplementedException"/>.
/// </summary>
public sealed class ProjectStoreReal : IProjectStore
{
    // ------------------------------------------------------------------ //
    // Songs
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public Task<Song?> LoadAsync(string path)
        => throw new NotImplementedException("TODO: charger un morceau depuis un fichier JSON.");

    /// <inheritdoc/>
    public Task SaveAsync(Song song, string path)
        => throw new NotImplementedException("TODO: sauvegarder un morceau en JSON sur disque.");

    /// <inheritdoc/>
    public Song CreateNew(string title = "Nouveau morceau")
        => throw new NotImplementedException("TODO: créer un nouveau morceau avec les valeurs par défaut.");

    /// <inheritdoc/>
    public IReadOnlyList<Song> GetAll()
        => throw new NotImplementedException("TODO: retourner tous les morceaux chargés.");

    /// <inheritdoc/>
    public void Update(Song song)
        => throw new NotImplementedException("TODO: mettre à jour un morceau existant.");

    /// <inheritdoc/>
    public bool Delete(Guid songId)
        => throw new NotImplementedException("TODO: supprimer un morceau par son identifiant.");

    // ------------------------------------------------------------------ //
    // Playlists
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public Playlist CreatePlaylist(string name = "Playlist")
        => throw new NotImplementedException("TODO: créer une nouvelle playlist.");

    /// <inheritdoc/>
    public IReadOnlyList<Playlist> GetAllPlaylists()
        => throw new NotImplementedException("TODO: retourner toutes les playlists.");

    /// <inheritdoc/>
    public void UpdatePlaylist(Playlist playlist)
        => throw new NotImplementedException("TODO: mettre à jour une playlist existante.");

    /// <inheritdoc/>
    public bool DeletePlaylist(Guid playlistId)
        => throw new NotImplementedException("TODO: supprimer une playlist par son identifiant.");

    // ------------------------------------------------------------------ //
    // Settings
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public AppSettings GetSettings()
        => throw new NotImplementedException("TODO: charger les paramètres globaux.");

    /// <inheritdoc/>
    public void SaveSettings(AppSettings settings)
        => throw new NotImplementedException("TODO: sauvegarder les paramètres globaux.");
}
