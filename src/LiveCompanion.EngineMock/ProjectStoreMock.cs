using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Validation;
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
    public Task<LoadResult<Song>> LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validation = new ValidationResult();

        string? json;
        lock (_lock)
            _store.TryGetValue(path, out json);

        if (json is null)
        {
            validation.AddError("path", $"Fichier introuvable : '{path}'.");
            _log.Debug(LogSource.EngineMock, $"[ProjectStore] Load '{path}' → not found");
            return Task.FromResult(new LoadResult<Song>(null, validation));
        }

        Song? song;
        try
        {
            song = JsonSerializer.Deserialize<Song>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            validation.AddError("json", $"JSON malformé : {ex.Message}");
            return Task.FromResult(new LoadResult<Song>(null, validation));
        }

        if (song is null)
        {
            validation.AddError("json", "Désérialisation a retourné null.");
            return Task.FromResult(new LoadResult<Song>(null, validation));
        }

        var modelValidation = ModelValidator.ValidateSong(song);
        if (!modelValidation.IsValid)
            return Task.FromResult(new LoadResult<Song>(null, modelValidation));

        validation.Merge(modelValidation);
        _log.Debug(LogSource.EngineMock, $"[ProjectStore] Load '{path}' → '{song.Title}'");
        return Task.FromResult(new LoadResult<Song>(song, validation));
    }

    /// <inheritdoc/>
    public Task<ValidationResult> SaveAsync(Song song, string path)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validation = ModelValidator.ValidateSong(song);
        if (!validation.IsValid)
        {
            _log.Warn(LogSource.EngineMock, $"[ProjectStore] Save '{path}' annulé — validation échouée");
            return Task.FromResult(validation);
        }

        song.LastModified = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(song, _jsonOptions);

        lock (_lock)
            _store[path] = json;

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] Save '{path}' ← '{song.Title}' ({song.Sections.Count} sections)");
        return Task.FromResult(validation);
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

    /// <inheritdoc/>
    public Task<ValidationResult> SaveAllSongsAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        List<Song> songs;
        lock (_lock)
            songs = _songs.Values.ToList();

        var validation = new ValidationResult();
        foreach (var song in songs)
        {
            var sv = ModelValidator.ValidateSong(song);
            validation.Merge(sv);
        }

        if (!validation.IsValid)
            return Task.FromResult(validation);

        var json = JsonSerializer.Serialize(songs, _jsonOptions);
        lock (_lock)
            _store[path] = json;

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] SaveAllSongs '{path}' ← {songs.Count} morceaux");
        return Task.FromResult(validation);
    }

    /// <inheritdoc/>
    public Task<LoadResult<IReadOnlyList<Song>>> LoadAllSongsAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validation = new ValidationResult();

        string? json;
        lock (_lock)
            _store.TryGetValue(path, out json);

        if (json is null)
        {
            validation.AddError("path", $"Fichier introuvable : '{path}'.");
            return Task.FromResult(new LoadResult<IReadOnlyList<Song>>(null, validation));
        }

        List<Song>? songs;
        try
        {
            songs = JsonSerializer.Deserialize<List<Song>>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            validation.AddError("json", $"JSON malformé : {ex.Message}");
            return Task.FromResult(new LoadResult<IReadOnlyList<Song>>(null, validation));
        }

        if (songs is null)
        {
            validation.AddError("json", "Désérialisation a retourné null.");
            return Task.FromResult(new LoadResult<IReadOnlyList<Song>>(null, validation));
        }

        foreach (var song in songs)
        {
            var sv = ModelValidator.ValidateSong(song);
            validation.Merge(sv);
        }

        if (!validation.IsValid)
            return Task.FromResult(new LoadResult<IReadOnlyList<Song>>(null, validation));

        lock (_lock)
        {
            foreach (var song in songs)
                _songs[song.Id] = song;
        }

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] LoadAllSongs '{path}' → {songs.Count} morceaux");
        return Task.FromResult(new LoadResult<IReadOnlyList<Song>>((IReadOnlyList<Song>)songs.AsReadOnly(), validation));
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

    /// <inheritdoc/>
    public Task<ValidationResult> SavePlaylistsAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        List<Playlist> playlists;
        IReadOnlyList<Song> songs;
        lock (_lock)
        {
            playlists = _playlists.Values.ToList();
            songs = _songs.Values.ToList();
        }

        var validation = ModelValidator.ValidatePlaylists(playlists, songs);
        if (!validation.IsValid)
            return Task.FromResult(validation);

        var json = JsonSerializer.Serialize(playlists, _jsonOptions);
        lock (_lock)
            _store[path] = json;

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] SavePlaylists '{path}' ← {playlists.Count} playlists");
        return Task.FromResult(validation);
    }

    /// <inheritdoc/>
    public Task<LoadResult<IReadOnlyList<Playlist>>> LoadPlaylistsAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validation = new ValidationResult();

        string? json;
        lock (_lock)
            _store.TryGetValue(path, out json);

        if (json is null)
        {
            validation.AddError("path", $"Fichier introuvable : '{path}'.");
            return Task.FromResult(new LoadResult<IReadOnlyList<Playlist>>(null, validation));
        }

        List<Playlist>? playlists;
        try
        {
            playlists = JsonSerializer.Deserialize<List<Playlist>>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            validation.AddError("json", $"JSON malformé : {ex.Message}");
            return Task.FromResult(new LoadResult<IReadOnlyList<Playlist>>(null, validation));
        }

        if (playlists is null)
        {
            validation.AddError("json", "Désérialisation a retourné null.");
            return Task.FromResult(new LoadResult<IReadOnlyList<Playlist>>(null, validation));
        }

        IReadOnlyList<Song> songs;
        lock (_lock)
            songs = _songs.Values.ToList();

        var playlistValidation = ModelValidator.ValidatePlaylists(playlists, songs);
        validation.Merge(playlistValidation);

        lock (_lock)
        {
            foreach (var pl in playlists)
                _playlists[pl.Id] = pl;
        }

        _log.Debug(LogSource.EngineMock, $"[ProjectStore] LoadPlaylists '{path}' → {playlists.Count} playlists");
        return Task.FromResult(new LoadResult<IReadOnlyList<Playlist>>((IReadOnlyList<Playlist>)playlists.AsReadOnly(), validation));
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
