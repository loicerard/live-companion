using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveCompanion.EngineMock;

/// <summary>
/// Simule la persistance de projets en mémoire (pas d'I/O fichier réel).
/// Utilise <see cref="JsonSerializer"/> pour sérialiser/désérialiser les morceaux,
/// ce qui garantit des copies indépendantes à chaque load/save (deep copy).
/// Thread-safe : les dictionnaires internes sont protégés par un <c>lock</c>.
/// </summary>
public sealed class ProjectStoreMock : IProjectStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILogService _log;
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _store = []; // path → JSON
    private readonly Dictionary<Guid, Song> _songs = [];     // id → Song
    private readonly Dictionary<Guid, Playlist> _playlists = []; // id → Playlist
    private AppSettings _settings = new();

    public ProjectStoreMock(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

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
    // IProjectStore — Songs
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
            _log.Debug(LogSource.EngineMock, $"[ProjectStore] Load '{path}' → not found");
            return Task.FromResult<Song?>(null);
        }

        var song = JsonSerializer.Deserialize<Song>(json, _jsonOptions);
        _log.Debug(LogSource.EngineMock, $"[ProjectStore] Load '{path}' → '{song?.Title}'");
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

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] Save '{path}' ← '{song.Title}' ({song.Sections.Count} sections)");
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

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] CreateNew '{title}' — {song.Sections.Count} sections par défaut");
        return song;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Song> GetAll()
    {
        lock (_lock)
            return _songs.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public void Update(Song song)
    {
        ArgumentNullException.ThrowIfNull(song);

        song.LastModified = DateTime.UtcNow;

        lock (_lock)
            _songs[song.Id] = song;

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] Update '{song.Title}' — " +
                        $"{song.Sections.Count} sections, " +
                        $"{song.AudioClips.Count} clips, " +
                        $"{song.MidiEvents.Count} MIDI events");
    }

    /// <inheritdoc/>
    public bool Delete(Guid songId)
    {
        bool removed;
        lock (_lock)
            removed = _songs.Remove(songId);

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] Delete '{songId}' → {(removed ? "OK" : "not found")}");
        return removed;
    }

    // ------------------------------------------------------------------ //
    // IProjectStore — Playlists
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public Playlist CreatePlaylist(string name = "Playlist")
    {
        var playlist = new Playlist { Name = name };

        lock (_lock)
            _playlists[playlist.Id] = playlist;

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] CreatePlaylist '{name}'");
        return playlist;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Playlist> GetAllPlaylists()
    {
        lock (_lock)
            return _playlists.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public void UpdatePlaylist(Playlist playlist)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        lock (_lock)
            _playlists[playlist.Id] = playlist;

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] UpdatePlaylist '{playlist.Name}' — {playlist.SongIds.Count} songs");
    }

    /// <inheritdoc/>
    public bool DeletePlaylist(Guid playlistId)
    {
        bool removed;
        lock (_lock)
            removed = _playlists.Remove(playlistId);

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] DeletePlaylist '{playlistId}' → {(removed ? "OK" : "not found")}");
        return removed;
    }

    // ------------------------------------------------------------------ //
    // IProjectStore — Settings
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public AppSettings GetSettings()
    {
        lock (_lock)
            return _settings;
    }

    /// <inheritdoc/>
    public void SaveSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_lock)
            _settings = settings;

        _log.Debug(LogSource.EngineMock, "[ProjectStore] SaveSettings — " +
                        $"audio={settings.AudioConfig?.DriverName ?? "null"}, " +
                        $"midi ports={settings.MidiConfig?.SelectedPorts?.Count ?? 0}");
    }
}
