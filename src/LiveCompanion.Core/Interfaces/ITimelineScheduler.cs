using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Contrat du scheduler de timeline. Orchestre l'enchaînement des sections,
/// déclenche les samples et les événements MIDI aux positions précises.
/// </summary>
public interface ITimelineScheduler
{
    /// <summary>Position courante dans la timeline.</summary>
    TimelinePosition CurrentPosition { get; }

    /// <summary>
    /// Indique si le scheduler est en état de passer à la section suivante
    /// immédiatement (aucun sample en cours, ou stop récent).
    /// </summary>
    bool CanTransitionNow { get; }

    /// <summary>
    /// Déclenché à chaque avancement de la position (tick, temps, mesure).
    /// </summary>
    event EventHandler<TimelinePosition> PositionChanged;

    /// <summary>
    /// Déclenché quand la section courante change.
    /// </summary>
    event EventHandler<int> SectionChanged;

    /// <summary>
    /// Démarre le scheduling du morceau depuis la section indiquée.
    /// </summary>
    /// <param name="song">Morceau à exécuter.</param>
    /// <param name="startSectionIndex">Index de la section de départ (0-based).</param>
    Task StartAsync(Song song, int startSectionIndex = 0);

    /// <summary>
    /// Arrête le scheduling et remet la position à zéro.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Passe à la section suivante selon les règles V1 :
    /// transition immédiate si <see cref="CanTransitionNow"/> est vrai,
    /// ignorée sinon.
    /// </summary>
    Task NextSectionAsync();
}
