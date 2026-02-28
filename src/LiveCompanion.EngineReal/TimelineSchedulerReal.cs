using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineReal;

/// <summary>
/// Implémentation réelle du scheduler de timeline.
/// Stub : toutes les méthodes lèvent <see cref="NotImplementedException"/>.
/// </summary>
public sealed class TimelineSchedulerReal : ITimelineScheduler
{
    /// <inheritdoc/>
    public TimelinePosition CurrentPosition
        => throw new NotImplementedException("TODO: retourner la position courante du scheduler.");

    /// <inheritdoc/>
    public bool CanTransitionNow
        => throw new NotImplementedException("TODO: vérifier si une transition immédiate est possible.");

    /// <inheritdoc/>
    public event EventHandler<TimelinePosition>? PositionChanged;

    /// <inheritdoc/>
    public event EventHandler<int>? SectionChanged;

    /// <inheritdoc/>
    public Task StartAsync(Song song, int startSectionIndex = 0)
        => throw new NotImplementedException("TODO: démarrer le scheduling du morceau.");

    /// <inheritdoc/>
    public Task StopAsync()
        => throw new NotImplementedException("TODO: arrêter le scheduler et remettre la position à zéro.");

    /// <inheritdoc/>
    public Task NextSectionAsync()
        => throw new NotImplementedException("TODO: passer à la section suivante.");
}
