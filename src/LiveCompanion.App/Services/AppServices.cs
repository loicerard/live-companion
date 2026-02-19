using LiveCompanion.Audio;
using LiveCompanion.Audio.Abstractions;
using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;
using LiveCompanion.Midi;
using LiveCompanion.Midi.Abstractions;

namespace LiveCompanion.App.Services;

/// <summary>
/// Composition root: creates and wires all engine services.
/// Owned by App; exposed to ViewModels via constructor injection.
/// </summary>
public sealed class AppServices : IDisposable
{
    private bool _disposed;

    // ── Audio ──────────────────────────────────────────────────────
    public AsioService AsioService { get; }
    public MetronomeAudioEngine? MetronomeAudio { get; private set; }
    public SamplePlayer? SamplePlayer { get; private set; }

    // ── Core ───────────────────────────────────────────────────────
    public MetronomeEngine Metronome { get; }
    public SetlistPlayer Player { get; }

    // ── MIDI ───────────────────────────────────────────────────────
    public MidiService MidiService { get; }
    public MidiRouter MidiRouter { get; }
    public MidiClockEngine? MidiClock { get; private set; }
    public MidiInputHandler MidiInput { get; }

    // ── Configs ────────────────────────────────────────────────────
    public AudioConfiguration AudioConfig { get; private set; }
    public MidiConfiguration MidiConfig { get; private set; }

    public AppServices(AudioConfiguration audioConfig, MidiConfiguration midiConfig)
    {
        AudioConfig = audioConfig;
        MidiConfig = midiConfig;

        // Core
        Metronome = new MetronomeEngine(Setlist.DefaultPpqn, 120);
        Player    = new SetlistPlayer(Metronome);

        // Audio (ASIO service always created; initialized lazily when driver is configured)
        AsioService = new AsioService(new NAudioAsioOutFactory(), audioConfig);

        // MIDI
        MidiService = new MidiService(new NAudioMidiPortFactory(), midiConfig);
        MidiRouter  = new MidiRouter(MidiService, midiConfig);
        MidiRouter.Attach(Player);

        MidiInput = new MidiInputHandler(new NAudioMidiPortFactory(), midiConfig);
    }

    /// <summary>
    /// Initialises the ASIO driver and creates audio engines.
    /// Must be called after the driver name is configured.
    /// </summary>
    public void InitializeAudio()
    {
        AsioService.Initialize();
        AsioService.Play();

        MetronomeAudio = new MetronomeAudioEngine(AsioService, AudioConfig,
                                                   Setlist.DefaultPpqn, 120);
        SamplePlayer = new SamplePlayer(AsioService, AudioConfig);
        SamplePlayer.SubscribeTo(Player);

        // Keep MetronomeAudio in sync with section tempo changes
        Player.SectionChanged += e => MetronomeAudio.ChangeTempo(e.Bpm, e.TimeSignature);

        // Wire MIDI Clock to ASIO tick source
        MidiClock = new MidiClockEngine(MidiService, MidiConfig, Setlist.DefaultPpqn);
        MidiClock.Attach(MetronomeAudio, Player);
    }

    /// <summary>
    /// Updates the audio configuration and saves it to disk.
    /// </summary>
    public void UpdateAudioConfig(AudioConfiguration config)
    {
        AudioConfig = config;
        AsioService.UpdateConfiguration(config);
    }

    /// <summary>
    /// Updates the MIDI configuration and saves it to disk.
    /// </summary>
    public void UpdateMidiConfig(MidiConfiguration config)
    {
        MidiConfig = config;
        MidiService.UpdateConfiguration(config);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        MidiClock?.Dispose();
        MidiInput.Dispose();
        MidiRouter.Dispose();
        MidiService.Dispose();
        SamplePlayer?.Dispose();
        AsioService.Dispose();
    }
}
