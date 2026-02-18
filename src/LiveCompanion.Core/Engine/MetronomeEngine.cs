using LiveCompanion.Core.Models;

namespace LiveCompanion.Core.Engine;

/// <summary>
/// Advances a tick counter at the rate determined by the current BPM and PPQN.
/// Phase 1 uses Task.Delay; Phase 2 will swap in a high-precision ASIO callback.
/// </summary>
public sealed class MetronomeEngine
{
    private readonly int _ppqn;
    private int _bpm;
    private TimeSignature _timeSignature = TimeSignature.Common;
    private CancellationTokenSource? _cts;
    private Task? _runLoop;

    public MetronomeEngine(int ppqn, int initialBpm)
    {
        _ppqn = ppqn;
        _bpm = initialBpm;
    }

    public long CurrentTick { get; private set; }
    public bool IsRunning => _cts is { IsCancellationRequested: false };

    /// <summary>
    /// Fired on every tick advance. Subscribers receive the current tick.
    /// </summary>
    public event Action<long>? TickAdvanced;

    /// <summary>
    /// Fired on every beat boundary (every PPQN ticks).
    /// Parameters: beat number (0-based within current bar), bar number (0-based).
    /// </summary>
    public event Action<int, int>? Beat;

    public void ChangeTempo(int bpm, TimeSignature timeSignature)
    {
        _bpm = bpm;
        _timeSignature = timeSignature;
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        CurrentTick = 0;
        _runLoop = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Wait for the internal loop to finish after Stop() is called.
    /// </summary>
    public async Task WaitForStopAsync()
    {
        if (_runLoop is not null)
            await _runLoop.ConfigureAwait(false);
    }

    /// <summary>
    /// Advance exactly one tick. Used by SetlistPlayer to drive the engine
    /// synchronously in a controlled loop. Returns the new tick value.
    /// </summary>
    internal long AdvanceTick()
    {
        CurrentTick++;
        TickAdvanced?.Invoke(CurrentTick);
        CheckBeat();
        return CurrentTick;
    }

    internal void Reset()
    {
        CurrentTick = 0;
    }

    private void CheckBeat()
    {
        if (CurrentTick % _ppqn != 0) return;
        var totalBeats = (int)(CurrentTick / _ppqn);
        var beatsPerBar = _timeSignature.Numerator;
        var bar = totalBeats / beatsPerBar;
        var beat = totalBeats % beatsPerBar;
        Beat?.Invoke(beat, bar);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var intervalMs = GetTickIntervalMs();
                await Task.Delay(TimeSpan.FromMilliseconds(intervalMs), ct).ConfigureAwait(false);
                AdvanceTick();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private double GetTickIntervalMs()
    {
        // BPM = quarter-notes per minute
        // One quarter-note = PPQN ticks
        // Interval per tick = 60_000 ms / (BPM * PPQN)
        return 60_000.0 / (_bpm * _ppqn);
    }
}
