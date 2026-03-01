using LiveCompanion.Core.Interfaces;

namespace LiveCompanion.Core.Services;

/// <summary>
/// Implémentation par défaut de <see cref="ILogService"/>.
/// Écrit chaque entrée dans <see cref="System.Diagnostics.Debug.WriteLine(string)"/>
/// et conserve un buffer circulaire en mémoire (max <see cref="MaxEntries"/>).
/// Thread-safe.
/// </summary>
public sealed class DebugLogService : ILogService
{
    /// <summary>Nombre maximum d'entrées conservées en mémoire.</summary>
    public const int MaxEntries = 1000;

    private readonly object _lock = new();
    private readonly LinkedList<LogEntry> _entries = new();

    /// <inheritdoc/>
    public event Action<LogEntry>? EntryAdded;

    /// <inheritdoc/>
    public void Log(LogLevel level, LogSource source, string message)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, source, message);

        lock (_lock)
        {
            _entries.AddLast(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveFirst();
        }

        System.Diagnostics.Debug.WriteLine($"[{source}] {level}: {message}");

        EntryAdded?.Invoke(entry);
    }

    /// <inheritdoc/>
    public IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_lock)
            return _entries.ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_lock)
            _entries.Clear();
    }
}
