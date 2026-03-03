using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Implémentation réelle de la persistance projet (JSON sur disque).
/// Thread-safe : les dictionnaires internes sont protégés par un <c>lock</c>.
/// </summary>
public sealed class ProjectStoreReal : IProjectStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ILogService _log;
    private readonly string _settingsPath;
    private readonly object _lock = new();
    private readonly Dictionary<Guid, Song> _songs = [];
    private readonly Dictionary<Guid, Playlist> _playlists = [];
    private AppSettings _settings = new();

    public ProjectStoreReal(ILogService log, string settingsPath)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _settingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));

        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                _log.Debug(LogSource.EngineReal, $"[ProjectStore] Settings chargés depuis '{_settingsPath}'");
            }
            catch (Exception ex)
            {
                _log.Warn(LogSource.EngineReal, $"[ProjectStore] Impossible de charger les settings : {ex.Message}");
            }
        }
    }

    // ------------------------------------------------------------------ //
    // IProjectStore — Songs
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public async Task<Song?> LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            _log.Debug(LogSource.EngineReal, $"[ProjectStore] Load '{path}' → not found");
            return null;
        }

        var json = await File.ReadAllTextAsync(path);
        var song = JsonSerializer.Deserialize<Song>(json, _jsonOptions);

        if (song != null)
            lock (_lock) _songs[song.Id] = song;

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] Load '{path}' → '{song?.Title}'");
        return song;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(Song song, string path)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        song.LastModified = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(song, _jsonOptions);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, json);
        _log.Debug(LogSource.EngineReal, $"[ProjectStore] Save '{path}' ← '{song.Title}' ({song.Sections.Count} sections)");
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

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] CreateNew '{title}' — {song.Sections.Count} sections par défaut");
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

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] Update '{song.Title}' — " +
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

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] Delete '{songId}' → {(removed ? "OK" : "not found")}");
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

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] CreatePlaylist '{name}'");
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

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] UpdatePlaylist '{playlist.Name}' — {playlist.SongIds.Count} songs");
    }

    /// <inheritdoc/>
    public bool DeletePlaylist(Guid playlistId)
    {
        bool removed;
        lock (_lock)
            removed = _playlists.Remove(playlistId);

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] DeletePlaylist '{playlistId}' → {(removed ? "OK" : "not found")}");
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

        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var dir = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(_settingsPath, json);

        lock (_lock)
            _settings = settings;

        _log.Debug(LogSource.EngineReal, "[ProjectStore] SaveSettings — " +
                        $"audio={settings.AudioConfig?.DriverName ?? "null"}, " +
                        $"midi ports={settings.MidiConfig?.SelectedPorts?.Count ?? 0}");
    }
}
