using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using System.Timers;
using Timer = System.Timers.Timer;

namespace LiveCompanion.EngineMock;

/// <summary>
/// Simule le scheduler de timeline sans moteur audio/MIDI réel.
/// Avance les beats via un <see cref="Timer"/> et gère l'enchaînement automatique des sections.
/// Déclenche les <see cref="AudioClip"/> et <see cref="MidiEvent"/> aux positions correspondantes.
/// <para>
/// Thread-safe : les mutations d'état sont sérialisées dans un <c>lock</c> ;
/// les événements <see cref="PositionChanged"/> et <see cref="SectionChanged"/> sont levés
/// hors du lock pour éviter les deadlocks.
/// </para>
/// Implémenter <see cref="IDisposable"/> est nécessaire car la classe possède un <see cref="Timer"/>.
/// </summary>
public sealed class TimelineSchedulerMock : ITimelineScheduler, IDisposable
{
    // ------------------------------------------------------------------ //
    // État interne
    // ------------------------------------------------------------------ //

    private readonly object _lock = new();
    private readonly Timer _timer;
    private readonly ILogService _log;

    /// <summary>
    /// Délégué optionnel qui indique si des voix audio sont actives.
    /// Utilisé pour calculer <see cref="CanTransitionNow"/>.
    /// Injection par constructeur pour découpler l'AudioEngineMock.
    /// </summary>
    private readonly Func<bool> _hasActiveVoices;

    private readonly IAudioEngine? _audioEngine;
    private readonly IMidiEngine? _midiEngine;
    private readonly IProjectStore? _projectStore;

    private Song? _song;
    private int _sectionIndex;
    private int _bar;   // 1-based
    private int _beat;  // 1-based
    private bool _running;

    // ------------------------------------------------------------------ //
    // Constructeurs
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Crée un scheduler mock.
    /// </summary>
    /// <param name="log">Service de logging.</param>
    /// <param name="hasActiveVoices">
    /// Délégué retournant <c>true</c> si des voix audio fictives sont en cours de lecture.
    /// Permet à <see cref="CanTransitionNow"/> de refléter l'état audio.
    /// Si <c>null</c>, le scheduler considère qu'il n'y a jamais de voix actives.
    /// </param>
    /// <param name="audioEngine">Moteur audio pour déclencher les samples aux positions de la timeline.</param>
    /// <param name="midiEngine">Moteur MIDI pour envoyer les événements aux positions de la timeline.</param>
    public TimelineSchedulerMock(
        ILogService log,
        Func<bool>? hasActiveVoices = null,
        IAudioEngine? audioEngine = null,
        IMidiEngine? midiEngine = null,
        IProjectStore? projectStore = null)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _hasActiveVoices = hasActiveVoices ?? (() => false);
        _audioEngine = audioEngine;
        _midiEngine = midiEngine;
        _projectStore = projectStore;
        _timer = new Timer { AutoReset = false }; // intervalle recalculé à chaque tick
        _timer.Elapsed += OnTimerElapsed;
    }

    // ------------------------------------------------------------------ //
    // ITimelineScheduler — propriétés
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public TimelinePosition CurrentPosition
    {
        get
        {
            lock (_lock)
                return new TimelinePosition(_sectionIndex, _bar, _beat, 0);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>true</c> quand le transport n'est pas en cours (<see cref="_running"/> = false)
    /// OU qu'aucune voix audio n'est active.
    /// </remarks>
    public bool CanTransitionNow => !_running || !_hasActiveVoices();

    // ------------------------------------------------------------------ //
    // ITimelineScheduler — événements
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public event EventHandler<TimelinePosition>? PositionChanged;

    /// <inheritdoc/>
    public event EventHandler<int>? SectionChanged;

    // ------------------------------------------------------------------ //
    // ITimelineScheduler — méthodes
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public async Task StartAsync(Song song, int startSectionIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(song);
        if (song.Sections.Count == 0)
            throw new ArgumentException("Le morceau ne contient aucune section.", nameof(song));

        startSectionIndex = Math.Clamp(startSectionIndex, 0, song.Sections.Count - 1);

        // Preload all audio clips (+ click track) before starting playback
        if (_audioEngine is not null)
        {
            var paths = song.AudioClips
                .Select(c => c.FilePath)
                .Where(p => !string.IsNullOrWhiteSpace(p));

            if (!string.IsNullOrWhiteSpace(song.ClickTrackPath))
                paths = paths.Append(song.ClickTrackPath);

            var distinct = paths.Distinct(StringComparer.OrdinalIgnoreCase);
            await _audioEngine.PreloadAsync(distinct).ConfigureAwait(false);
        }

        lock (_lock)
        {
            _song = song;
            _sectionIndex = startSectionIndex;
            _bar = 1;
            _beat = 1;
            _running = true;
            ArmTimer(song.Sections[startSectionIndex].Tempo);
        }

        _log.Debug(LogSource.EngineMock, $"[Scheduler] Start — section={startSectionIndex} '{song.Sections[startSectionIndex].Name}'");

        // Lancer la piste de clic dès le départ (joue pendant tout le morceau)
        if (!string.IsNullOrWhiteSpace(song.ClickTrackPath) && _audioEngine is not null)
        {
            var clickClip = new AudioClip
            {
                Name = "__click_track__",
                FilePath = song.ClickTrackPath,
                Sends = [new BusSend { BusName = "Click", Volume = 1.0 }],
                Position = TimelinePosition.Zero,
            };
            _ = _audioEngine.PlayClipAsync(clickClip);
            _log.Debug(LogSource.EngineMock, "[Scheduler] Click track started");
        }

        var pos = CurrentPosition;

        // Déclencher les clips/événements à la position de départ
        TriggerEventsAtPosition(song, pos);

        PositionChanged?.Invoke(this, pos);
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        lock (_lock)
        {
            _timer.Stop();
            _running = false;
            _bar = 1;
            _beat = 1;
            // _sectionIndex est conservé pour pouvoir reprendre depuis la même section
        }

        if (_audioEngine is not null)
            await _audioEngine.StopAllAsync().ConfigureAwait(false);

        _log.Debug(LogSource.EngineMock, "[Scheduler] Stop");

        var pos = CurrentPosition;
        PositionChanged?.Invoke(this, pos);
    }

    /// <inheritdoc/>
    public Task NextSectionAsync()
    {
        if (!CanTransitionNow)
        {
            _log.Debug(LogSource.EngineMock, "[Scheduler] NextSection ignored — CanTransitionNow=false");
            return Task.CompletedTask;
        }

        int? newSection = null;
        bool shouldStop = false;

        lock (_lock)
        {
            if (_song is null || !_running)
                return Task.CompletedTask;

            int next = _sectionIndex + 1;
            if (next >= _song.Sections.Count)
            {
                // Dernière section atteinte → stop
                _timer.Stop();
                _running = false;
                _bar = 1;
                _beat = 1;
                shouldStop = true;
            }
            else
            {
                _sectionIndex = next;
                _bar = 1;
                _beat = 1;
                newSection = next;
                ArmTimer(_song.Sections[next].Tempo);
            }
        }

        if (shouldStop)
        {
            _log.Debug(LogSource.EngineMock, "[Scheduler] NextSection → end of song, stopping");
            PositionChanged?.Invoke(this, CurrentPosition);
        }
        else if (newSection.HasValue)
        {
            _log.Debug(LogSource.EngineMock, $"[Scheduler] NextSection → section={newSection}");
            SectionChanged?.Invoke(this, newSection.Value);
            PositionChanged?.Invoke(this, CurrentPosition);
        }

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // Timer
    // ------------------------------------------------------------------ //

    private void ArmTimer(double bpm)
    {
        // Intervalle = durée d'un beat en ms
        _timer.Interval = 60_000.0 / bpm;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        TimelinePosition? pos = null;
        int? sectionChanged = null;
        bool stopped = false;
        Song? song = null;

        lock (_lock)
        {
            if (!_running || _song is null)
                return;

            song = _song;
            var section = _song.Sections[_sectionIndex];

            _beat++;
            if (_beat > section.TimeSignature.Numerator)
            {
                _beat = 1;
                _bar++;
            }

            if (_bar > section.BarCount)
            {
                // Fin de section → passage automatique à la suivante
                int next = _sectionIndex + 1;
                if (next >= _song.Sections.Count)
                {
                    _running = false;
                    _bar = 1;
                    _beat = 1;
                    stopped = true;
                }
                else
                {
                    _sectionIndex = next;
                    _bar = 1;
                    _beat = 1;
                    sectionChanged = next;
                    ArmTimer(_song.Sections[next].Tempo);
                }
            }
            else
            {
                ArmTimer(section.Tempo);
            }

            pos = new TimelinePosition(_sectionIndex, _bar, _beat, 0);
        }

        // Lever les événements hors du lock
        if (sectionChanged.HasValue)
        {
            _log.Debug(LogSource.EngineMock, $"[Scheduler] Auto-advance → section={sectionChanged}");
            SectionChanged?.Invoke(this, sectionChanged.Value);
        }

        if (pos is not null)
        {
            // Déclencher les AudioClips et MidiEvents à cette position
            if (song is not null)
                TriggerEventsAtPosition(song, pos);

            PositionChanged?.Invoke(this, pos);
        }

        if (stopped)
        {
            _log.Debug(LogSource.EngineMock, "[Scheduler] End of song — auto-stopped");
            PositionChanged?.Invoke(this, CurrentPosition);
        }
    }

    // ------------------------------------------------------------------ //
    // Déclenchement AudioClips / MidiEvents
    // ------------------------------------------------------------------ //

    private void TriggerEventsAtPosition(Song song, TimelinePosition pos)
    {
        // AudioClips
        foreach (var clip in song.AudioClips)
        {
            if (!ShouldTriggerClip(clip, pos))
                continue;

            try
            {
                _ = _audioEngine?.PlayClipAsync(clip);
                _log.Debug(LogSource.EngineMock,
                    $"[Scheduler] Trigger AudioClip '{clip.Name}' " +
                    $"at {pos.SectionIndex}:{pos.Bar}:{pos.Beat}");
            }
            catch (InvalidOperationException ex)
            {
                _log.Warn(LogSource.EngineMock,
                    $"[Scheduler] AudioClip '{clip.Name}' skipped — {ex.Message}");
            }
        }

        // MidiEvents
        var profiles = _projectStore?.GetSettings().MidiProfiles ?? [];
        foreach (var evt in song.MidiEvents)
        {
            if (evt.Position.SectionIndex != pos.SectionIndex ||
                evt.Position.Bar != pos.Bar ||
                evt.Position.Beat != pos.Beat)
                continue;

            try
            {
                _ = _midiEngine?.SendEventAsync(evt, profiles);
                _log.Debug(LogSource.EngineMock,
                    $"[Scheduler] Trigger MidiEvent {evt.Type} " +
                    $"profiles=[{string.Join(",", evt.ProfileIds)}] " +
                    $"at {pos.SectionIndex}:{pos.Bar}:{pos.Beat}");
            }
            catch (InvalidOperationException ex)
            {
                _log.Warn(LogSource.EngineMock,
                    $"[Scheduler] MidiEvent skipped — {ex.Message}");
            }
        }
    }

    private static bool ShouldTriggerClip(AudioClip clip, TimelinePosition pos)
    {
        if (clip.Position.SectionIndex != pos.SectionIndex)
            return false;

        return clip.SyncMode switch
        {
            // Free : déclencher quand section, mesure et temps correspondent
            SyncMode.Free =>
                clip.Position.Bar == pos.Bar && clip.Position.Beat == pos.Beat,

            // BarAligned : déclencher uniquement sur le premier temps de la mesure
            SyncMode.BarAligned =>
                clip.Position.Bar == pos.Bar && pos.Beat == 1,

            _ => false,
        };
    }

    // ------------------------------------------------------------------ //
    // IDisposable
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public void Dispose()
    {
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Stop();
        _timer.Dispose();
    }
}
