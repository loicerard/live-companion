namespace LiveCompanion.Core.Models;

/// <summary>
/// Conteneur pour l'export/import centralisé de toute la configuration :
/// settings, morceaux et playlists.
/// </summary>
public class FullExport
{
    /// <summary>Paramètres globaux de l'application.</summary>
    public AppSettings Settings { get; set; } = new();

    /// <summary>Tous les morceaux.</summary>
    public List<Song> Songs { get; set; } = [];

    /// <summary>Toutes les playlists.</summary>
    public List<Playlist> Playlists { get; set; } = [];
}
