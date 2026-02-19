using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Engine;

/// <summary>
/// State machine that drives the playback of a <see cref="Setlist"/>.
/// Dispatches domain events as the tick counter advances through each song's timeline.
/// </summary>
public sealed class SetlistPlayer
{
    private readonly MetronomeEngine _metronome;
    private Setlist? _setlist;
    private int _songIndex;
    private CancellationTokenSource? _cts;
    private Task? _playbackLoop;

    public SetlistPlayer(MetronomeEngine metronome)
    {
        _metronome = metronome;
    }

    public PlayerState State { get; private set; } = PlayerState.Idle;
    public int CurrentSongIndex => _songIndex;
    public Song? CurrentSong => _setlist?.Songs.ElementAtOrDefault(_songIndex);

    // ── Domain events ──────────────────────────────────────────────

    public event Action<Song, int>? SongStarted;
    public event Action<Song, int>? SongEnded;
    public event Action? SetlistCompleted;
    public event Action<int, int>? BeatFired;
    public event Action<SectionChangeEvent>? SectionChanged;
    public event Action<AudioCueEvent>? AudioCueFired;
    public event Action<MidiPreset>? MidiPresetChanged;

    // ── Public API ─────────────────────────────────────────────────

    public void Load(Setlist setlist)
    {
        if (State == PlayerState.Playing)
            throw new InvalidOperationException("Cannot load a setlist while playing.");

        _setlist = setlist ?? throw new ArgumentNullException(nameof(setlist));
        State = PlayerState.Idle;
        _songIndex = 0;
    }

    /// <summary>
    /// Start playback from the beginning of the loaded setlist.
    /// The internal loop uses Task.Delay-based timing (Phase 1).
    /// </summary>
    public void Play()
    {
        if (_setlist is null || _setlist.Songs.Count == 0)
            throw new InvalidOperationException("No setlist loaded.");
        if (State == PlayerState.Playing)
            return;

        State = PlayerState.Playing;
        _songIndex = 0;
        _cts = new CancellationTokenSource();
        _playbackLoop = RunPlaybackAsync(_cts.Token);
    }

    /// <summary>
    /// Emergency stop — halts playback immediately.
    /// </summary>
    public void Stop()
    {
        if (State != PlayerState.Playing) return;
        _cts?.Cancel();
        _metronome.Stop();
        State = PlayerState.Stopped;
    }

    /// <summary>
    /// Await completion or cancellation of the playback loop.
    /// </summary>
    public async Task WaitForCompletionAsync()
    {
        if (_playbackLoop is not null)
            await _playbackLoop.ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous, non-timed tick-by-tick driver for testing.
    /// Plays the entire setlist dispatching events at each tick without any delay.
    /// </summary>
    public void PlaySynchronous()
    {
        if (_setlist is null || _setlist.Songs.Count == 0)
            throw new InvalidOperationException("No setlist loaded.");

        State = PlayerState.Playing;
        _songIndex = 0;

        _metronome.Beat += OnBeat;
        try
        {
            for (; _songIndex < _setlist.Songs.Count; _songIndex++)
            {
                PlaySongSynchronous(_setlist.Songs[_songIndex]);
            }

            State = PlayerState.Stopped;
            SetlistCompleted?.Invoke();
        }
        finally
        {
            _metronome.Beat -= OnBeat;
        }
    }

    /// <summary>
    /// Stop mid-song during synchronous playback (for testing).
    /// </summary>
    internal bool SynchronousStopRequested { get; set; }

    // ── Private ────────────────────────────────────────────────────

    private async Task RunPlaybackAsync(CancellationToken ct)
    {
        _metronome.Beat += OnBeat;
        try
        {
            for (; _songIndex < _setlist!.Songs.Count && !ct.IsCancellationRequested; _songIndex++)
            {
                await PlaySongAsync(_setlist.Songs[_songIndex], ct).ConfigureAwait(false);
            }

            if (!ct.IsCancellationRequested)
            {
                State = PlayerState.Stopped;
                SetlistCompleted?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            // Stop() was called
        }
        finally
        {
            _metronome.Beat -= OnBeat;
        }
    }

    private async Task PlaySongAsync(Song song, CancellationToken ct)
    {
        BeginSong(song);

        var sortedEvents = song.Events.OrderBy(e => e.Tick).ToList();
        int eventIdx = 0;

        while (_metronome.CurrentTick < song.DurationTicks && !ct.IsCancellationRequested)
        {
            var intervalMs = 60_000.0 / (_getCurrentBpm() * _setlist!.Ppqn);
            await Task.Delay(TimeSpan.FromMilliseconds(intervalMs), ct).ConfigureAwait(false);
            _metronome.AdvanceTick();

            eventIdx = DispatchDueEvents(sortedEvents, eventIdx, _metronome.CurrentTick);
        }

        EndSong(song);
    }

    private void PlaySongSynchronous(Song song)
    {
        BeginSong(song);

        var sortedEvents = song.Events.OrderBy(e => e.Tick).ToList();
        int eventIdx = 0;

        while (_metronome.CurrentTick < song.DurationTicks)
        {
            if (SynchronousStopRequested)
            {
                State = PlayerState.Stopped;
                return;
            }

            _metronome.AdvanceTick();
            eventIdx = DispatchDueEvents(sortedEvents, eventIdx, _metronome.CurrentTick);
        }

        EndSong(song);
    }

    private void BeginSong(Song song)
    {
        _metronome.Reset();

        // Apply initial section if the first event is at tick 0
        var firstSection = song.Events
            .OfType<SectionChangeEvent>()
            .OrderBy(e => e.Tick)
            .FirstOrDefault();
        if (firstSection is not null)
        {
            _metronome.ChangeTempo(firstSection.Bpm, firstSection.TimeSignature);
        }

        SongStarted?.Invoke(song, _songIndex);
    }

    private void EndSong(Song song)
    {
        SongEnded?.Invoke(song, _songIndex);
    }

    private int DispatchDueEvents(List<SongEvent> sortedEvents, int startIdx, long currentTick)
    {
        int idx = startIdx;
        while (idx < sortedEvents.Count && sortedEvents[idx].Tick <= currentTick)
        {
            DispatchEvent(sortedEvents[idx]);
            idx++;
        }
        return idx;
    }

    private void DispatchEvent(SongEvent evt)
    {
        switch (evt)
        {
            case SectionChangeEvent section:
                _metronome.ChangeTempo(section.Bpm, section.TimeSignature);
                SectionChanged?.Invoke(section);
                foreach (var preset in section.Presets)
                {
                    MidiPresetChanged?.Invoke(preset);
                }
                break;

            case AudioCueEvent cue:
                AudioCueFired?.Invoke(cue);
                break;
        }
    }

    private int _getCurrentBpm()
    {
        // Fallback BPM if no section has been dispatched yet
        return 120;
    }

    private void OnBeat(int beat, int bar)
    {
        BeatFired?.Invoke(beat, bar);
    }
}
