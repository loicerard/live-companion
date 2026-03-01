namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Niveaux de sévérité des logs.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>
/// Sources des messages de log.
/// </summary>
public enum LogSource
{
    Core,
    EngineMock,
    EngineReal,
    UI,
}

/// <summary>
/// Entrée de log immuable.
/// </summary>
public sealed record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    LogSource Source,
    string Message);

/// <summary>
/// Service de logging structuré. Collecte les messages avec niveau, source et horodatage.
/// Expose un buffer en mémoire pour affichage dans une console de debug interne.
/// </summary>
public interface ILogService
{
    /// <summary>
    /// Enregistre un message de log.
    /// </summary>
    void Log(LogLevel level, LogSource source, string message);

    /// <summary>
    /// Événement levé quand une nouvelle entrée est ajoutée.
    /// </summary>
    event Action<LogEntry>? EntryAdded;

    /// <summary>
    /// Retourne une copie des entrées de log en mémoire (les plus récentes en dernier).
    /// </summary>
    IReadOnlyList<LogEntry> GetEntries();

    /// <summary>
    /// Vide le buffer de logs en mémoire.
    /// </summary>
    void Clear();
}

/// <summary>
/// Méthodes d'extension pour simplifier l'appel au <see cref="ILogService"/>.
/// </summary>
public static class LogServiceExtensions
{
    public static void Debug(this ILogService log, LogSource source, string message)
        => log.Log(LogLevel.Debug, source, message);

    public static void Info(this ILogService log, LogSource source, string message)
        => log.Log(LogLevel.Info, source, message);

    public static void Warn(this ILogService log, LogSource source, string message)
        => log.Log(LogLevel.Warning, source, message);

    public static void Error(this ILogService log, LogSource source, string message)
        => log.Log(LogLevel.Error, source, message);
}
