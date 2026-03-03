using LiveCompanion.Core.Interfaces;

namespace LiveCompanion.Core.Services;

/// <summary>
/// Implémentation du garde de sécurité du mode Live.
/// Thread-safe via <c>volatile</c>.
/// </summary>
public sealed class LiveModeGuard : ILiveModeGuard
{
    private readonly ILogService _log;
    private volatile bool _isLive;

    public LiveModeGuard(ILogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc/>
    public bool IsLive => _isLive;

    /// <inheritdoc/>
    public event EventHandler<bool>? LiveModeChanged;

    /// <inheritdoc/>
    public void Engage()
    {
        if (_isLive) return;
        _isLive = true;
        _log.Info(LogSource.Core, "[LiveModeGuard] Mode Live ACTIVÉ — édition bloquée");
        LiveModeChanged?.Invoke(this, true);
    }

    /// <inheritdoc/>
    public void Disengage()
    {
        if (!_isLive) return;
        _isLive = false;
        _log.Info(LogSource.Core, "[LiveModeGuard] Mode Live DÉSACTIVÉ — édition autorisée");
        LiveModeChanged?.Invoke(this, false);
    }
}
