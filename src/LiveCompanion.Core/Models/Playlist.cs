namespace LiveCompanion.Core.Models;

/// <summary>
/// Liste ordonnée de morceaux à jouer en séquence.
/// </summary>
public class Playlist
{
    /// <summary>Identifiant unique de la playlist.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Nom affiché de la playlist.</summary>
    public string Name { get; set; } = "Playlist";

    /// <summary>Liste ordonnée des identifiants de morceaux.</summary>
    public List<Guid> SongIds { get; init; } = [];

    /// <summary>Date de création.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
