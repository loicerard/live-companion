using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveCompanion.EngineMock;

/// <summary>
/// Simule la persistance de projets en mémoire (pas d'I/O fichier réel).
/// Utilise <see cref="JsonSerializer"/> pour sérialiser/désérialiser les morceaux,
/// ce qui garantit des copies indépendantes à chaque load/save (deep copy).
/// Thread-safe : le dictionnaire interne est protégé par un <c>lock</c>.
/// </summary>
public sealed class ProjectStoreMock : IProjectStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _lock = new();
    private readonly Dictionary<string, string> _store = []; // path → JSON
    private readonly Dictionary<Guid, Song> _songs = [];     // id → Song

    // ------------------------------------------------------------------ //
    // Propriété utilitaire
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Chemins fictifs actuellement stockés en mémoire.
    /// Utile pour les tests et le debug.
    /// </summary>
    public IReadOnlyList<string> StoredPaths
    {
        get { lock (_lock) return _store.Keys.ToList().AsReadOnly(); }
    }

    // ------------------------------------------------------------------ //
    // IProjectStore
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public Task<Song?> LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? json;
        lock (_lock)
            _store.TryGetValue(path, out json);

        if (json is null)
        {
            Debug.WriteLine($"[ProjectStoreMock] Load '{path}' → not found");
            return Task.FromResult<Song?>(null);
        }

        var song = JsonSerializer.Deserialize<Song>(json, _jsonOptions);
        Debug.WriteLine($"[ProjectStoreMock] Load '{path}' → '{song?.Title}'");
        return Task.FromResult<Song?>(song);
    }

    /// <inheritdoc/>
    public Task SaveAsync(Song song, string path)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        song.LastModified = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(song, _jsonOptions);

        lock (_lock)
            _store[path] = json;

        Debug.WriteLine($"[ProjectStoreMock] Save '{path}' ← '{song.Title}' ({song.Sections.Count} sections)");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Song CreateNew(string title = "Nouveau morceau")
    {
        var song = new Song
        {
            Title = title,
            Sections =
            {
                new Section { Name = "Intro",   Tempo = 120, TimeSignature = TimeSignature.Default, BarCount = 4, Order = 0 },
                new Section { Name = "Couplet", Tempo = 120, TimeSignature = TimeSignature.Default, BarCount = 4, Order = 1 },
                new Section { Name = "Refrain", Tempo = 120, TimeSignature = TimeSignature.Default, BarCount = 4, Order = 2 },
            },
        };

        lock (_lock)
            _songs[song.Id] = song;

        Debug.WriteLine($"[ProjectStoreMock] CreateNew '{title}' — {song.Sections.Count} sections par défaut");
        return song;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Song> GetAll()
    {
        lock (_lock)
            return _songs.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public bool Delete(Guid songId)
    {
        bool removed;
        lock (_lock)
            removed = _songs.Remove(songId);

        Debug.WriteLine($"[ProjectStoreMock] Delete '{songId}' → {(removed ? "OK" : "not found")}");
        return removed;
    }
}
