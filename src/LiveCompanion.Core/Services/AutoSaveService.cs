using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Services;

/// <summary>
/// Sauvegarde automatique périodique des morceaux modifiés.
/// Compare les timestamps <see cref="Song.LastModified"/> pour détecter les changements.
/// Sauvegarde dans <c>{saveFolderPath}/{songId}.json</c>.
/// Thread-safe.
/// </summary>
public sealed class AutoSaveService : IAutoSaveService
{
    private readonly IProjectStore _store;
    private readonly ILogService _log;
    private readonly string _saveFolderPath;
    private readonly object _lock = new();

    private Timer? _timer;
    private volatile bool _running;
    private volatile bool _saving;

    /// <summary>Dernières dates de sauvegarde par morceau.</summary>
    private readonly Dictionary<Guid, DateTime> _lastSavedAt = [];

    public AutoSaveService(IProjectStore store, ILogService log, string saveFolderPath)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _saveFolderPath = saveFolderPath ?? throw new ArgumentNullException(nameof(saveFolderPath));
    }

    /// <inheritdoc/>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public bool IsRunning => _running;

    /// <inheritdoc/>
    public void Start()
    {
        lock (_lock)
        {
            if (_running) return;

            _timer = new Timer(OnTimerElapsed, null, Interval, Interval);
            _running = true;
        }

        _log.Info(LogSource.Core, $"[AutoSave] Started — interval={Interval.TotalMinutes:F0}min, folder='{_saveFolderPath}'");
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _running = false;
        }

        _log.Info(LogSource.Core, "[AutoSave] Stopped");
    }

    /// <inheritdoc/>
    public async Task SaveNowAsync()
    {
        if (_saving) return; // Pas de sauvegarde concurrente
        _saving = true;

        try
        {
            var songs = _store.GetAll();
            int saved = 0;

            foreach (var song in songs)
            {
                bool needsSave;
                lock (_lock)
                {
                    needsSave = !_lastSavedAt.TryGetValue(song.Id, out var lastSaved)
                                || song.LastModified > lastSaved;
                }

                if (!needsSave) continue;

                var path = GetSongPath(song.Id);
                try
                {
                    var result = await _store.SaveAsync(song, path);
                    if (!result.IsValid)
                    {
                        _log.Warn(LogSource.Core,
                            $"[AutoSave] Validation échouée pour '{song.Title}' — " +
                            string.Join("; ", result.Issues.Select(i => i.Message)));
                        continue;
                    }
                    lock (_lock)
                        _lastSavedAt[song.Id] = song.LastModified;
                    saved++;
                }
                catch (Exception ex)
                {
                    _log.Error(LogSource.Core,
                        $"[AutoSave] Erreur sauvegarde '{song.Title}' — {ex.Message}");
                }
            }

            // Sauvegarde des playlists
            try
            {
                var playlistPath = Path.Combine(_saveFolderPath, "playlists.json");
                await _store.SavePlaylistsAsync(playlistPath);
            }
            catch (Exception ex)
            {
                _log.Error(LogSource.Core, $"[AutoSave] Erreur sauvegarde playlists — {ex.Message}");
            }

            // Nettoyer les entrées orphelines (morceaux supprimés)
            lock (_lock)
            {
                var currentIds = new HashSet<Guid>(songs.Select(s => s.Id));
                var orphanIds = _lastSavedAt.Keys.Where(id => !currentIds.Contains(id)).ToList();
                foreach (var id in orphanIds)
                    _lastSavedAt.Remove(id);
            }

            if (saved > 0)
                _log.Info(LogSource.Core, $"[AutoSave] {saved} morceau(x) sauvegardé(s)");
            else
                _log.Debug(LogSource.Core, "[AutoSave] Aucune modification détectée");
        }
        finally
        {
            _saving = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //

    private void OnTimerElapsed(object? state)
    {
        _ = SaveNowAsync();
    }

    private string GetSongPath(Guid songId)
        => Path.Combine(_saveFolderPath, $"{songId}.json");
}
