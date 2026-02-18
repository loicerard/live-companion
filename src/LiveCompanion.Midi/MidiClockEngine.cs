using LiveCompanion.Audio;
using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiveCompanion.Midi;

/// <summary>
/// Generates MIDI Clock messages (0xF8) synchronized to the ASIO audio callback.
///
/// Key design: this engine hooks into <see cref="MetronomeAudioEngine.TickAdvanced"/>,
/// which fires on the ASIO audio thread inside <c>MetronomeWaveProvider.Read()</c>.
/// This makes the MIDI clock pixel-perfect with the audio metronome — no separate
/// timer, no drift.
///
/// MIDI Clock spec (MIDI 1.0):
///   - 24 Timing Clock messages (0xF8) per quarter note
///   - PPQN = 480 → one clock every 480 / 24 = 20 ticks
///   - Start (0xFA): sent when playback begins
///   - Continue (0xFB): sent when playback resumes after a pause
///   - Stop (0xFC): sent when playback stops
///
/// All clock messages are sent via <see cref="MidiService.SendImmediate"/> (no queueing)
/// so that stale clocks are never replayed on reconnect.
///
/// BPM changes (via <see cref="SetlistPlayer.SectionChanged"/>) are handled implicitly:
/// the tick rate of <see cref="MetronomeAudioEngine"/> changes, so the interval between
/// TickAdvanced callbacks changes, and therefore the MIDI clock rate changes automatically.
/// </summary>
public sealed class MidiClockEngine : IDisposable
{
    // MIDI spec: 24 timing clocks per quarter note
    internal const int ClocksPerQuarterNote = 24;

    // MIDI system realtime messages (single byte, packed as int)
    internal const int MidiClock    = 0xF8;
    internal const int MidiStart    = 0xFA;
    internal const int MidiContinue = 0xFB;
    internal const int MidiStop     = 0xFC;

    private readonly MidiService _midiService;
    private readonly MidiConfiguration _config;
    private readonly int _ppqn;
    private readonly ILogger<MidiClockEngine> _logger;

    // How many ticks between each MIDI clock pulse
    // At PPQN=480: ticksPerClock = 480 / 24 = 20
    private readonly int _ticksPerClock;

    private MetronomeAudioEngine? _metronome;
    private SetlistPlayer? _player;

    // _isRunning is written from the main thread (Start/Stop) and read from the ASIO
    // callback thread (OnTickAdvanced). Volatile ensures visibility across threads.
    private volatile bool _isRunning;

    // _tickSinceStart is incremented only from the ASIO callback thread.
    // Reset from the main thread via Volatile.Write to ensure the ASIO thread
    // sees the zero before the next increment.
    private long _tickSinceStart;
    private bool _disposed;

    public MidiClockEngine(MidiService midiService, MidiConfiguration config, int ppqn,
        ILogger<MidiClockEngine>? logger = null)
    {
        _midiService = midiService ?? throw new ArgumentNullException(nameof(midiService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _ppqn = ppqn;
        _logger = logger ?? NullLogger<MidiClockEngine>.Instance;

        if (ppqn % ClocksPerQuarterNote != 0)
            throw new ArgumentException(
                $"PPQN ({ppqn}) must be divisible by {ClocksPerQuarterNote} (MIDI clock pulses per quarter note).",
                nameof(ppqn));

        _ticksPerClock = ppqn / ClocksPerQuarterNote;
    }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>Number of PPQN ticks between consecutive MIDI clock pulses.</summary>
    public int TicksPerClock => _ticksPerClock;

    /// <summary>
    /// Attaches the engine to a <see cref="MetronomeAudioEngine"/> (tick source)
    /// and optionally to a <see cref="SetlistPlayer"/> for BPM-change notifications.
    ///
    /// The tick callback fires on the ASIO audio thread — MIDI clock pulses are sent
    /// directly from that thread via <see cref="MidiService.SendImmediate"/>.
    /// </summary>
    public void Attach(MetronomeAudioEngine metronome, SetlistPlayer? player = null)
    {
        Detach();

        _metronome = metronome;
        _metronome.TickAdvanced += OnTickAdvanced;

        _player = player;
        // SectionChanged carries the new BPM — no explicit action needed here because
        // the tick rate of MetronomeAudioEngine changes automatically via SetlistPlayer.
        // We subscribe only to log BPM changes for diagnostics.
        if (_player is not null)
            _player.SectionChanged += OnSectionChanged;
    }

    /// <summary>Detaches from the metronome and player.</summary>
    public void Detach()
    {
        if (_metronome is not null)
        {
            _metronome.TickAdvanced -= OnTickAdvanced;
            _metronome = null;
        }
        if (_player is not null)
        {
            _player.SectionChanged -= OnSectionChanged;
            _player = null;
        }
    }

    /// <summary>
    /// Sends MIDI Start (0xFA) to all clock targets and begins emitting clocks.
    /// </summary>
    public void Start()
    {
        Volatile.Write(ref _tickSinceStart, 0L);
        _isRunning = true;
        SendToClockTargets(MidiStart);
        _logger.LogInformation("MIDI Clock started. Sending to: {Targets}",
            string.Join(", ", _config.ClockTargets));
    }

    /// <summary>
    /// Sends MIDI Stop (0xFC) to all clock targets and halts clock emission.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        SendToClockTargets(MidiStop);
        _logger.LogInformation("MIDI Clock stopped.");
    }

    /// <summary>
    /// Sends MIDI Continue (0xFB) to all clock targets and resumes clock emission.
    /// Does NOT reset the tick counter (continues from the paused position).
    /// </summary>
    public void Continue()
    {
        _isRunning = true;
        SendToClockTargets(MidiContinue);
        _logger.LogInformation("MIDI Clock continued.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
    }

    // ── ASIO callback (hot path) ──────────────────────────────────

    /// <summary>
    /// Called on the ASIO audio thread for every tick advance.
    /// Sends a MIDI clock pulse every <see cref="TicksPerClock"/> ticks.
    ///
    /// This is the hot path — keep it minimal. No allocations, no locks.
    /// NAudio's midiOutShortMsg Win32 call is safe from the audio thread.
    /// </summary>
    private void OnTickAdvanced(long tick)
    {
        if (!_isRunning) return;

        _tickSinceStart++;

        if (_tickSinceStart % _ticksPerClock == 0)
        {
            SendToClockTargets(MidiClock);
        }
    }

    private void OnSectionChanged(SectionChangeEvent section)
    {
        _logger.LogDebug(
            "MIDI Clock: BPM changed to {Bpm} at tick (handled implicitly via ASIO tick rate).",
            section.Bpm);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private void SendToClockTargets(int message)
    {
        foreach (var target in _config.ClockTargets)
        {
            if (!_config.OutputDevices.TryGetValue(target, out var deviceConfig))
                continue;

            _midiService.SendImmediate(deviceConfig.PortName, message);
        }
    }
}
