using System.Diagnostics;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.EngineReal;

/// <summary>
/// High-precision timeline scheduler using a dedicated thread and <see cref="Stopwatch"/>.
/// Advances position at tick-level granularity (480 ticks per beat, standard MIDI PPQ).
/// Triggers <see cref="AudioClip"/> and <see cref="MidiEvent"/> at the correct positions.
/// <para>
/// Thread-safe: state mutations are serialized within a <c>lock</c>;
/// events are raised outside the lock to prevent deadlocks.
/// </para>
/// </summary>
public sealed class TimelineSchedulerReal : ITimelineScheduler, IDisposable
{
    /// <summary>Ticks per beat (Pulses Per Quarter note — standard MIDI resolution).</summary>
    public const int TicksPerBeat = 480;

    // ------------------------------------------------------------------ //
    // Dependencies
    // ------------------------------------------------------------------ //

    private readonly ILogService _log;
    private readonly Func<bool> _hasActiveVoices;
    private readonly IAudioEngine? _audioEngine;
    private readonly IMidiEngine? _midiEngine;

    // ------------------------------------------------------------------ //
    // State
    // ------------------------------------------------------------------ //

    private readonly object _lock = new();
    private readonly Stopwatch _stopwatch = new();

    private Song? _song;
    private int _sectionIndex;
    private int _bar = 1;   // 1-based
    private int _beat = 1;  // 1-based
    private int _tick;       // 0-based, 0..TicksPerBeat-1
    private volatile bool _running;
    private long _lastTotalTicks;  // absolute ticks counted since Start

    private Thread? _thread;

    // ------------------------------------------------------------------ //
    // Constructor
    // ------------------------------------------------------------------ //

    public TimelineSchedulerReal(
        ILogService log,
        Func<bool>? hasActiveVoices = null,
        IAudioEngine? audioEngine = null,
        IMidiEngine? midiEngine = null)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _hasActiveVoices = hasActiveVoices ?? (() => false);
        _audioEngine = audioEngine;
        _midiEngine = midiEngine;
    }

    // ------------------------------------------------------------------ //
    // ITimelineScheduler — properties
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public TimelinePosition CurrentPosition
    {
        get
        {
            lock (_lock)
                return new TimelinePosition(_sectionIndex, _bar, _beat, _tick);
        }
    }

    /// <inheritdoc/>
    public bool CanTransitionNow => !_running || !_hasActiveVoices();

    // ------------------------------------------------------------------ //
    // ITimelineScheduler — events
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public event EventHandler<TimelinePosition>? PositionChanged;

    /// <inheritdoc/>
    public event EventHandler<int>? SectionChanged;

    // ------------------------------------------------------------------ //
    // ITimelineScheduler — methods
    // ------------------------------------------------------------------ //

    /// <inheritdoc/>
    public async Task StartAsync(Song song, int startSectionIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(song);
        if (song.Sections.Count == 0)
            throw new ArgumentException("Le morceau ne contient aucune section.", nameof(song));

        startSectionIndex = Math.Clamp(startSectionIndex, 0, song.Sections.Count - 1);

        // Preload all audio clips before starting playback
        if (_audioEngine is not null && song.AudioClips.Count > 0)
        {
            var paths = song.AudioClips
                .Select(c => c.FilePath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            await _audioEngine.PreloadAsync(paths).ConfigureAwait(false);
        }

        lock (_lock)
        {
            // Stop previous run if any
            StopThread();

            _song = song;
            _sectionIndex = startSectionIndex;
            _bar = 1;
            _beat = 1;
            _tick = 0;
            _lastTotalTicks = 0;
            _running = true;

            _stopwatch.Restart();

            _thread = new Thread(SchedulerLoop)
            {
                Name = "TimelineSchedulerReal",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
            };
            _thread.Start();
        }

        _log.Debug(LogSource.EngineReal,
            $"[Scheduler] Start — section={startSectionIndex} '{song.Sections[startSectionIndex].Name}'");

        var pos = CurrentPosition;
        TriggerEventsAtPosition(song, pos);
        PositionChanged?.Invoke(this, pos);
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        lock (_lock)
        {
            StopThread();
            _bar = 1;
            _beat = 1;
            _tick = 0;
        }

        if (_audioEngine is not null)
            await _audioEngine.StopAllAsync().ConfigureAwait(false);

        _log.Debug(LogSource.EngineReal, "[Scheduler] Stop");
        PositionChanged?.Invoke(this, CurrentPosition);
    }

    /// <inheritdoc/>
    public Task NextSectionAsync()
    {
        if (!CanTransitionNow)
        {
            _log.Debug(LogSource.EngineReal, "[Scheduler] NextSection ignored — CanTransitionNow=false");
            return Task.CompletedTask;
        }

        int? newSection = null;
        bool shouldStop = false;
        Song? song = null;

        lock (_lock)
        {
            if (_song is null || !_running)
                return Task.CompletedTask;

            song = _song;
            int next = _sectionIndex + 1;

            if (next >= _song.Sections.Count)
            {
                StopThread();
                _bar = 1;
                _beat = 1;
                _tick = 0;
                shouldStop = true;
            }
            else
            {
                _sectionIndex = next;
                _bar = 1;
                _beat = 1;
                _tick = 0;
                newSection = next;

                // Reset timing for the new section's tempo
                _lastTotalTicks = 0;
                _stopwatch.Restart();
            }
        }

        if (shouldStop)
        {
            _log.Debug(LogSource.EngineReal, "[Scheduler] NextSection → end of song, stopping");
            PositionChanged?.Invoke(this, CurrentPosition);
        }
        else if (newSection.HasValue)
        {
            _log.Debug(LogSource.EngineReal, $"[Scheduler] NextSection → section={newSection}");

            if (song is not null)
                TriggerEventsAtPosition(song, CurrentPosition);

            SectionChanged?.Invoke(this, newSection.Value);
            PositionChanged?.Invoke(this, CurrentPosition);
        }

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ //
    // Scheduler thread
    // ------------------------------------------------------------------ //

    private void SchedulerLoop()
    {
        while (_running)
        {
            TimelinePosition? posToRaise = null;
            int? sectionToRaise = null;
            bool stoppedAtEnd = false;
            Song? song = null;

            lock (_lock)
            {
                if (!_running || _song is null)
                    break;

                song = _song;
                var section = _song.Sections[_sectionIndex];
                double tempo = section.Tempo;
                var timeSig = section.TimeSignature;

                // Calculate how many ticks should have elapsed
                long targetTotalTicks = CalculateTotalTicks(_stopwatch.Elapsed, tempo);
                long ticksToAdvance = targetTotalTicks - _lastTotalTicks;

                if (ticksToAdvance > 0)
                {
                    for (long t = 0; t < ticksToAdvance && _running; t++)
                    {
                        _tick++;

                        if (_tick >= TicksPerBeat)
                        {
                            _tick = 0;
                            _beat++;

                            if (_beat > timeSig.Numerator)
                            {
                                _beat = 1;
                                _bar++;

                                if (_bar > section.BarCount)
                                {
                                    // End of section → auto-advance
                                    int next = _sectionIndex + 1;
                                    if (next >= _song.Sections.Count)
                                    {
                                        _running = false;
                                        _bar = 1;
                                        _beat = 1;
                                        _tick = 0;
                                        stoppedAtEnd = true;
                                        break;
                                    }
                                    else
                                    {
                                        _sectionIndex = next;
                                        _bar = 1;
                                        _beat = 1;
                                        _tick = 0;
                                        sectionToRaise = next;

                                        // Recalculate tempo for new section
                                        section = _song.Sections[next];
                                        tempo = section.Tempo;
                                        timeSig = section.TimeSignature;

                                        // Reset timing reference for new tempo
                                        _lastTotalTicks = 0;
                                        _stopwatch.Restart();
                                        targetTotalTicks = 0;
                                        break; // process remaining ticks on next iteration
                                    }
                                }
                            }

                            // Trigger events on beat boundaries (tick == 0)
                            if (_tick == 0 && _running)
                            {
                                var beatPos = new TimelinePosition(_sectionIndex, _bar, _beat, 0);
                                TriggerEventsAtPositionLocked(song, beatPos);
                            }
                        }
                    }

                    _lastTotalTicks = targetTotalTicks;
                    posToRaise = new TimelinePosition(_sectionIndex, _bar, _beat, _tick);
                }
            }

            // Raise events outside lock
            if (sectionToRaise.HasValue)
            {
                _log.Debug(LogSource.EngineReal, $"[Scheduler] Auto-advance → section={sectionToRaise}");
                SectionChanged?.Invoke(this, sectionToRaise.Value);
            }

            if (posToRaise is not null)
            {
                PositionChanged?.Invoke(this, posToRaise);
            }

            if (stoppedAtEnd)
            {
                _log.Debug(LogSource.EngineReal, "[Scheduler] End of song — auto-stopped");
                _stopwatch.Stop();
                PositionChanged?.Invoke(this, CurrentPosition);
                break;
            }

            // Sleep ~1ms for high-precision timing without burning CPU
            Thread.Sleep(1);
        }
    }

    // ------------------------------------------------------------------ //
    // Timing calculations
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Converts elapsed time to absolute tick count based on the current tempo.
    /// </summary>
    private static long CalculateTotalTicks(TimeSpan elapsed, double bpm)
    {
        double secondsPerBeat = 60.0 / bpm;
        double secondsPerTick = secondsPerBeat / TicksPerBeat;
        return (long)(elapsed.TotalSeconds / secondsPerTick);
    }

    // ------------------------------------------------------------------ //
    // Event triggering (AudioClips / MidiEvents)
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
                _log.Debug(LogSource.EngineReal,
                    $"[Scheduler] Trigger AudioClip '{clip.Name}' at {pos}");
            }
            catch (Exception ex)
            {
                _log.Warn(LogSource.EngineReal,
                    $"[Scheduler] AudioClip '{clip.Name}' skipped — {ex.Message}");
            }
        }

        // MidiEvents
        TriggerMidiAtPosition(song, pos);
    }

    /// <summary>
    /// Same as <see cref="TriggerEventsAtPosition"/> but called from within the lock.
    /// Only triggers events — does not raise PositionChanged.
    /// </summary>
    private void TriggerEventsAtPositionLocked(Song song, TimelinePosition pos)
    {
        // AudioClips
        foreach (var clip in song.AudioClips)
        {
            if (!ShouldTriggerClip(clip, pos))
                continue;

            try
            {
                _ = _audioEngine?.PlayClipAsync(clip);
            }
            catch (Exception)
            {
                // Logged on beat boundary in the main path
            }
        }

        // MidiEvents
        TriggerMidiAtPosition(song, pos);
    }

    private void TriggerMidiAtPosition(Song song, TimelinePosition pos)
    {
        foreach (var evt in song.MidiEvents)
        {
            if (evt.Position.SectionIndex != pos.SectionIndex ||
                evt.Position.Bar != pos.Bar ||
                evt.Position.Beat != pos.Beat)
                continue;

            try
            {
                _ = _midiEngine?.SendEventAsync(evt);
                _log.Debug(LogSource.EngineReal,
                    $"[Scheduler] Trigger MidiEvent {evt.Type} ch.{evt.Channel} at {pos}");
            }
            catch (Exception ex)
            {
                _log.Warn(LogSource.EngineReal,
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
            SyncMode.Free =>
                clip.Position.Bar == pos.Bar && clip.Position.Beat == pos.Beat,
            SyncMode.BarAligned =>
                clip.Position.Bar == pos.Bar && pos.Beat == 1,
            _ => false,
        };
    }

    // ------------------------------------------------------------------ //
    // Thread management
    // ------------------------------------------------------------------ //

    private void StopThread()
    {
        _running = false;
        _stopwatch.Stop();

        if (_thread is not null && _thread.IsAlive)
        {
            // Give the thread a chance to exit gracefully
            _thread.Join(timeout: TimeSpan.FromMilliseconds(100));
            _thread = null;
        }
    }

    // ------------------------------------------------------------------ //
    // IDisposable
    // ------------------------------------------------------------------ //

    public void Dispose()
    {
        lock (_lock)
            StopThread();
    }
}
