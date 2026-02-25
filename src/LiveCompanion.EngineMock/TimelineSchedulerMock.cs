using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using System.Diagnostics;
using System.Timers;
using Timer = System.Timers.Timer;

namespace LiveCompanion.EngineMock;

/// <summary>
/// Simule le scheduler de timeline sans moteur audio/MIDI réel.
/// Avance les beats via un <see cref="Timer"/> et gère l'enchaînement automatique des sections.
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

    /// <summary>
    /// Délégué optionnel qui indique si des voix audio sont actives.
    /// Utilisé pour calculer <see cref="CanTransitionNow"/>.
    /// Injection par constructeur pour découpler l'AudioEngineMock.
    /// </summary>
    private readonly Func<bool> _hasActiveVoices;

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
    /// <param name="hasActiveVoices">
    /// Délégué retournant <c>true</c> si des voix audio fictives sont en cours de lecture.
    /// Permet à <see cref="CanTransitionNow"/> de refléter l'état audio.
    /// Si <c>null</c>, le scheduler considère qu'il n'y a jamais de voix actives.
    /// </param>
    public TimelineSchedulerMock(Func<bool>? hasActiveVoices = null)
    {
        _hasActiveVoices = hasActiveVoices ?? (() => false);
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
    public Task StartAsync(Song song, int startSectionIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(song);
        if (song.Sections.Count == 0)
            throw new ArgumentException("Le morceau ne contient aucune section.", nameof(song));

        startSectionIndex = Math.Clamp(startSectionIndex, 0, song.Sections.Count - 1);

        lock (_lock)
        {
            _song = song;
            _sectionIndex = startSectionIndex;
            _bar = 1;
            _beat = 1;
            _running = true;
            ArmTimer(song.Sections[startSectionIndex].Tempo);
        }

        Debug.WriteLine($"[TimelineSchedulerMock] Start — section={startSectionIndex} '{song.Sections[startSectionIndex].Name}'");

        var pos = CurrentPosition;
        PositionChanged?.Invoke(this, pos);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        lock (_lock)
        {
            _timer.Stop();
            _running = false;
            _bar = 1;
            _beat = 1;
            // _sectionIndex est conservé pour pouvoir reprendre depuis la même section
        }

        Debug.WriteLine("[TimelineSchedulerMock] Stop");

        var pos = CurrentPosition;
        PositionChanged?.Invoke(this, pos);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task NextSectionAsync()
    {
        if (!CanTransitionNow)
        {
            Debug.WriteLine("[TimelineSchedulerMock] NextSection ignored — CanTransitionNow=false");
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
            Debug.WriteLine("[TimelineSchedulerMock] NextSection → end of song, stopping");
            PositionChanged?.Invoke(this, CurrentPosition);
        }
        else if (newSection.HasValue)
        {
            Debug.WriteLine($"[TimelineSchedulerMock] NextSection → section={newSection}");
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

        lock (_lock)
        {
            if (!_running || _song is null)
                return;

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
            Debug.WriteLine($"[TimelineSchedulerMock] Auto-advance → section={sectionChanged}");
            SectionChanged?.Invoke(this, sectionChanged.Value);
        }

        if (pos is not null)
            PositionChanged?.Invoke(this, pos);

        if (stopped)
        {
            Debug.WriteLine("[TimelineSchedulerMock] End of song — auto-stopped");
            PositionChanged?.Invoke(this, CurrentPosition);
        }
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
