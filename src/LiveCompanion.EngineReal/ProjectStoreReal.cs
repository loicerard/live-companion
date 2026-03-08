using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using LiveCompanion.Core.Validation;
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
    public async Task<LoadResult<Song>> LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validation = new ValidationResult();

        if (!File.Exists(path))
        {
            validation.AddError("path", $"Fichier introuvable : '{path}'.");
            _log.Debug(LogSource.EngineReal, $"[ProjectStore] Load '{path}' → not found");
            return new LoadResult<Song>(null, validation);
        }

        Song? song;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            song = JsonSerializer.Deserialize<Song>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            validation.AddError("json", $"JSON malformé dans '{path}' : {ex.Message}");
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] Load '{path}' → JSON invalide : {ex.Message}");
            return new LoadResult<Song>(null, validation);
        }

        if (song is null)
        {
            validation.AddError("json", $"Désérialisation de '{path}' a retourné null.");
            return new LoadResult<Song>(null, validation);
        }

        // Migration des anciens champs BusName/Volume vers Sends
        foreach (var clip in song.AudioClips)
            clip.MigrateLegacyFields();

        // Validation du modèle
        var modelValidation = ModelValidator.ValidateSong(song);
        if (!modelValidation.IsValid)
        {
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] Load '{path}' → validation échouée ({modelValidation.Issues.Count} erreurs)");
            return new LoadResult<Song>(null, modelValidation);
        }

        // Vérification des fichiers audio
        var fileValidation = ModelValidator.ValidateSongFiles(song);
        validation.Merge(modelValidation);
        validation.Merge(fileValidation);

        lock (_lock) _songs[song.Id] = song;

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] Load '{path}' → '{song.Title}'" +
            (validation.HasWarnings ? $" ({validation.Issues.Count} warnings)" : ""));
        return new LoadResult<Song>(song, validation);
    }

    /// <inheritdoc/>
    public async Task<ValidationResult> SaveAsync(Song song, string path)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validation = ModelValidator.ValidateSong(song);
        if (!validation.IsValid)
        {
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] Save '{path}' annulé — validation échouée ({validation.Issues.Count} erreurs)");
            return validation;
        }

        song.LastModified = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(song, _jsonOptions);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, json);
        _log.Debug(LogSource.EngineReal, $"[ProjectStore] Save '{path}' ← '{song.Title}' ({song.Sections.Count} sections)");
        return validation;
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

    /// <inheritdoc/>
    public async Task<ValidationResult> SaveAllSongsAsync(string path)
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
        {
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] SaveAllSongs annulé — validation échouée");
            return validation;
        }

        foreach (var song in songs)
            song.LastModified = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(songs, _jsonOptions);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, json);
        _log.Debug(LogSource.EngineReal, $"[ProjectStore] SaveAllSongs '{path}' ← {songs.Count} morceaux");
        return validation;
    }

    /// <inheritdoc/>
    public async Task<LoadResult<IReadOnlyList<Song>>> LoadAllSongsAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validation = new ValidationResult();

        if (!File.Exists(path))
        {
            validation.AddError("path", $"Fichier introuvable : '{path}'.");
            _log.Debug(LogSource.EngineReal, $"[ProjectStore] LoadAllSongs '{path}' → not found");
            return new LoadResult<IReadOnlyList<Song>>(null, validation);
        }

        List<Song>? songs;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            songs = JsonSerializer.Deserialize<List<Song>>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            validation.AddError("json", $"JSON malformé dans '{path}' : {ex.Message}");
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] LoadAllSongs '{path}' → JSON invalide : {ex.Message}");
            return new LoadResult<IReadOnlyList<Song>>(null, validation);
        }

        if (songs is null)
        {
            validation.AddError("json", $"Désérialisation de '{path}' a retourné null.");
            return new LoadResult<IReadOnlyList<Song>>(null, validation);
        }

        foreach (var song in songs)
        {
            foreach (var clip in song.AudioClips)
                clip.MigrateLegacyFields();

            var sv = ModelValidator.ValidateSong(song);
            validation.Merge(sv);
        }

        if (!validation.IsValid)
        {
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] LoadAllSongs '{path}' → validation échouée");
            return new LoadResult<IReadOnlyList<Song>>(null, validation);
        }

        lock (_lock)
        {
            foreach (var song in songs)
                _songs[song.Id] = song;
        }

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] LoadAllSongs '{path}' → {songs.Count} morceaux");
        return new LoadResult<IReadOnlyList<Song>>(songs.AsReadOnly(), validation);
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

    /// <inheritdoc/>
    public async Task<ValidationResult> SavePlaylistsAsync(string path)
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
        // On sauvegarde même avec des warnings (SongId manquant), mais pas si erreurs
        if (!validation.IsValid)
        {
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] SavePlaylists annulé — validation échouée");
            return validation;
        }

        var json = JsonSerializer.Serialize(playlists, _jsonOptions);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, json);
        _log.Debug(LogSource.EngineReal, $"[ProjectStore] SavePlaylists '{path}' ← {playlists.Count} playlists");
        return validation;
    }

    /// <inheritdoc/>
    public async Task<LoadResult<IReadOnlyList<Playlist>>> LoadPlaylistsAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validation = new ValidationResult();

        if (!File.Exists(path))
        {
            validation.AddError("path", $"Fichier introuvable : '{path}'.");
            _log.Debug(LogSource.EngineReal, $"[ProjectStore] LoadPlaylists '{path}' → not found");
            return new LoadResult<IReadOnlyList<Playlist>>(null, validation);
        }

        List<Playlist>? playlists;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            playlists = JsonSerializer.Deserialize<List<Playlist>>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            validation.AddError("json", $"JSON malformé dans '{path}' : {ex.Message}");
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] LoadPlaylists '{path}' → JSON invalide : {ex.Message}");
            return new LoadResult<IReadOnlyList<Playlist>>(null, validation);
        }

        if (playlists is null)
        {
            validation.AddError("json", $"Désérialisation de '{path}' a retourné null.");
            return new LoadResult<IReadOnlyList<Playlist>>(null, validation);
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

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] LoadPlaylists '{path}' → {playlists.Count} playlists" +
            (validation.HasWarnings ? $" ({validation.Issues.Count} warnings)" : ""));
        return new LoadResult<IReadOnlyList<Playlist>>(playlists.AsReadOnly(), validation);
    }

    // ------------------------------------------------------------------ //
    // IProjectStore — Export / Import centralisé
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public async Task<ValidationResult> SaveFullExportAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        List<Song> songs;
        List<Playlist> playlists;
        AppSettings settings;
        lock (_lock)
        {
            songs = _songs.Values.ToList();
            playlists = _playlists.Values.ToList();
            settings = _settings;
        }

        var validation = new ValidationResult();
        foreach (var song in songs)
            validation.Merge(ModelValidator.ValidateSong(song));

        validation.Merge(ModelValidator.ValidatePlaylists(playlists, songs));

        if (!validation.IsValid)
        {
            _log.Warn(LogSource.EngineReal, "[ProjectStore] SaveFullExport annulé — validation échouée");
            return validation;
        }

        foreach (var song in songs)
            song.LastModified = DateTime.UtcNow;

        var export = new FullExport
        {
            Settings = settings,
            Songs = songs,
            Playlists = playlists,
        };

        var json = JsonSerializer.Serialize(export, _jsonOptions);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, json);
        _log.Debug(LogSource.EngineReal, $"[ProjectStore] SaveFullExport '{path}' ← {songs.Count} morceaux, {playlists.Count} playlists");
        return validation;
    }

    /// <inheritdoc/>
    public async Task<LoadResult<FullExport>> LoadFullExportAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validation = new ValidationResult();

        if (!File.Exists(path))
        {
            validation.AddError("path", $"Fichier introuvable : '{path}'.");
            _log.Debug(LogSource.EngineReal, $"[ProjectStore] LoadFullExport '{path}' → not found");
            return new LoadResult<FullExport>(null, validation);
        }

        FullExport? export;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            export = JsonSerializer.Deserialize<FullExport>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            validation.AddError("json", $"JSON malformé dans '{path}' : {ex.Message}");
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] LoadFullExport '{path}' → JSON invalide : {ex.Message}");
            return new LoadResult<FullExport>(null, validation);
        }

        if (export is null)
        {
            validation.AddError("json", $"Désérialisation de '{path}' a retourné null.");
            return new LoadResult<FullExport>(null, validation);
        }

        // Valider les morceaux
        foreach (var song in export.Songs)
        {
            foreach (var clip in song.AudioClips)
                clip.MigrateLegacyFields();

            validation.Merge(ModelValidator.ValidateSong(song));
        }

        if (!validation.IsValid)
        {
            _log.Warn(LogSource.EngineReal, $"[ProjectStore] LoadFullExport '{path}' → validation échouée");
            return new LoadResult<FullExport>(null, validation);
        }

        // Valider les playlists
        validation.Merge(ModelValidator.ValidatePlaylists(export.Playlists, export.Songs));

        // Remplacer les données en mémoire
        lock (_lock)
        {
            _songs.Clear();
            foreach (var song in export.Songs)
                _songs[song.Id] = song;

            _playlists.Clear();
            foreach (var pl in export.Playlists)
                _playlists[pl.Id] = pl;

            _settings = export.Settings;
        }

        // Persister les settings sur disque
        SaveSettings(export.Settings);

        _log.Debug(LogSource.EngineReal, $"[ProjectStore] LoadFullExport '{path}' → {export.Songs.Count} morceaux, {export.Playlists.Count} playlists");
        return new LoadResult<FullExport>(export, validation);
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
