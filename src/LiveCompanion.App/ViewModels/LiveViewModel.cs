using System.Diagnostics;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;
using LiveCompanion.Audio;
using LiveCompanion.Audio.Abstractions;
using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// ViewModel for the Live (performance) view.
///
/// Bug 3 root causes addressed here:
/// 1. <see cref="AsioService.Initialize"/> is called before <see cref="Play"/> so
///    the router exists when <see cref="MetronomeAudioEngine"/> registers its source.
/// 2. <see cref="MetronomeAudioEngine.Start"/> is called after <see cref="AsioService.Play"/>
///    so the ASIO callback is already running when tick generation begins.
/// 3. The initial BPM comes from the first SectionChangeEvent at tick 0 (already fixed in
///    <see cref="SetlistPlayer.BeginSong"/>), not the hardcoded 120 fallback.
/// 4. Every <see cref="SetlistPlayer.SectionChanged"/> is forwarded to
///    <see cref="MetronomeAudioEngine.ChangeTempo"/> so the click track stays in sync.
/// 5. <see cref="SamplePlayer"/> uses <see cref="AppPathService.SamplesDirectory"/> so
///    relative file names in AudioCueEvents resolve correctly.
/// </summary>
public partial class LiveViewModel : ObservableObject, IDisposable
{
    private readonly ConfigViewModel _config;

    // Audio engine components (created fresh on each Play, torn down on Stop)
    private AsioService?        _asio;
    private MetronomeAudioEngine? _metronome;
    private SamplePlayer?       _samplePlayer;
    private MetronomeEngine?    _coreMetronome;
    private SetlistPlayer?      _setlistPlayer;

    private AudioConfiguration? _audioConfig;

    // ── Bindable properties ──────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isPlaying;

    [ObservableProperty]
    private string _currentSectionName = "—";

    [ObservableProperty]
    private int _currentBeat;

    [ObservableProperty]
    private int _currentBar;

    [ObservableProperty]
    private int _currentBpm;

    [ObservableProperty]
    private string _currentSong = "—";

    [ObservableProperty]
    private string _logOutput = string.Empty;

    private readonly StringBuilder _logBuffer = new();
    private readonly object _logLock = new();

    // ── Constructor ──────────────────────────────────────────────────────

    public LiveViewModel(ConfigViewModel config)
    {
        _config = config;
    }

    // ── Commands ─────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        var setlist = _config.GetSetlist();
        if (setlist.Songs.Count == 0)
        {
            MessageBox.Show("Aucune setlist chargée.", "Play", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _audioConfig = await TryLoadAudioConfigAsync();
            if (_audioConfig is null)
            {
                MessageBox.Show(
                    "Configuration audio introuvable.\nOuvrez l'onglet Configuration et sauvegardez vos paramètres.",
                    "Play", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_audioConfig.AsioDriverName))
            {
                MessageBox.Show(
                    "Aucun driver ASIO configuré.\nOuvrez l'onglet Configuration.",
                    "Play", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsPlaying = true;
            Log("=== Play ===");

            // ── Bug 3 step 3: Initialize ASIO before anything else ─────────
            _asio = new AsioService(new NAudioAsioOutFactory(), _audioConfig);
            _asio.AudioFault += msg => Log($"[ASIO FAULT] {msg}");
            _asio.Initialize();
            Log($"[ASIO] Initialized — driver: {_audioConfig.AsioDriverName}, " +
                $"buffer: {_audioConfig.BufferSize}, sr: {_audioConfig.SampleRate}");

            // ── Resolve initial BPM from tick-0 SectionChangeEvent ─────────
            int initialBpm = setlist.Songs[0].Events
                .OfType<SectionChangeEvent>()
                .OrderBy(e => e.Tick)
                .FirstOrDefault()?.Bpm ?? 120;
            Log($"[Play] Initial BPM from setlist: {initialBpm}");

            // ── Create MetronomeAudioEngine (registers itself on ASIO router) ──
            // Bug 3 step 5: engine registers source before Play() so the router
            // has a non-empty source list when the ASIO callback fires.
            _metronome = new MetronomeAudioEngine(
                _asio, _audioConfig, setlist.Ppqn, initialBpm);
            _metronome.Beat        += OnBeat;
            _metronome.TickAdvanced += OnTickAdvanced;
            Log("[MetronomeAudioEngine] Created and registered on ASIO router.");

            // ── Create SamplePlayer (registers itself on ASIO router) ──────
            // Bug 2: resolve relative sample paths from %AppData%\LiveCompanion\samples\
            _samplePlayer = new SamplePlayer(_asio, _audioConfig);
            _samplePlayer.LoadSetlistSamples(setlist, AppPathService.SamplesDirectory);
            Log($"[SamplePlayer] {_samplePlayer.LoadedSampleCount} sample(s) pre-loaded.");

            // ── Start ASIO playback — callback is now running ──────────────
            _asio.Play();
            Log("[ASIO] Play() called — ASIO callback is running.");

            // ── Bug 3 step 2: Start tick generation ────────────────────────
            _metronome.Start();
            Log("[MetronomeAudioEngine] Start() called — ticks flowing.");

            // ── Wire SetlistPlayer for event dispatch ──────────────────────
            // SetlistPlayer uses Core MetronomeEngine (Task.Delay timing) for
            // event dispatch. MetronomeAudioEngine drives the actual audio clock.
            _coreMetronome = new MetronomeEngine(setlist.Ppqn, initialBpm);
            _setlistPlayer = new SetlistPlayer(_coreMetronome);
            _setlistPlayer.SectionChanged    += OnSectionChanged;
            _setlistPlayer.SongStarted       += OnSongStarted;
            _setlistPlayer.SetlistCompleted  += OnSetlistCompleted;
            _samplePlayer.SubscribeTo(_setlistPlayer);

            // Bug 3 step 4: dispatch the tick-0 SectionChangeEvent before Play()
            // so MetronomeAudioEngine has the correct initial BPM.
            // (Already handled by SetlistPlayer.BeginSong() which seeds _currentBpm.)
            _setlistPlayer.Load(setlist);
            _setlistPlayer.Play();
            Log("[SetlistPlayer] Play() called.");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] {ex.Message}");
            Debug.WriteLine($"[Live] Play error: {ex}");
            await StopInternalAsync();
            MessageBox.Show($"Erreur au démarrage :\n{ex.Message}", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanPlay() => !IsPlaying;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        await StopInternalAsync();
    }

    private bool CanStop() => IsPlaying;

    // ── Event handlers ────────────────────────────────────────────────────

    // Bug 3 step 1: log every 20th tick (every MIDI clock pulse at PPQN=480)
    // to confirm ticks are advancing without flooding the log at 480 PPQN.
    private long _lastLoggedTick = -1;
    private void OnTickAdvanced(long tick)
    {
        if (tick - _lastLoggedTick >= 480)  // log once per beat
        {
            _lastLoggedTick = tick;
            Log($"[Tick] {tick}");
        }
    }

    private void OnBeat(int beat, int bar)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            CurrentBeat = beat;
            CurrentBar  = bar;
        });
        Debug.WriteLine($"[Live] Beat {beat} bar {bar}");
    }

    /// <summary>
    /// Bug 3 step 4 — SectionChanged → ChangeTempo:
    /// Keeps the ASIO-driven metronome click in sync with the setlist BPM.
    /// </summary>
    private void OnSectionChanged(SectionChangeEvent section)
    {
        Log($"[SectionChanged] {section.SectionName} — BPM={section.Bpm}");

        // Forward the new tempo to the ASIO-driven metronome
        _metronome?.ChangeTempo(section.Bpm, section.TimeSignature);

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            CurrentSectionName = section.SectionName;
            CurrentBpm         = section.Bpm;
        });
    }

    private void OnSongStarted(Song song, int index)
    {
        Log($"[Song] Start #{index + 1}: {song.Title}");
        Application.Current.Dispatcher.InvokeAsync(() => CurrentSong = song.Title);
    }

    private void OnSetlistCompleted()
    {
        Log("[Setlist] Completed.");
        Application.Current.Dispatcher.InvokeAsync(async () => await StopInternalAsync());
    }

    // ── Cleanup ──────────────────────────────────────────────────────────

    private async Task StopInternalAsync()
    {
        Log("=== Stop ===");

        if (_setlistPlayer is not null)
        {
            _setlistPlayer.Stop();
            _samplePlayer?.UnsubscribeFrom(_setlistPlayer);
        }

        _metronome?.Stop();
        _asio?.Stop();

        // Let the Task.Delay playback loop finish gracefully
        if (_setlistPlayer is not null)
        {
            try { await _setlistPlayer.WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { /* ignore timeout */ }
        }

        _samplePlayer?.Dispose();
        _asio?.Dispose();

        _setlistPlayer = null;
        _coreMetronome = null;
        _metronome     = null;
        _samplePlayer  = null;
        _asio          = null;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            IsPlaying          = false;
            CurrentSectionName = "—";
            CurrentSong        = "—";
            CurrentBeat        = 0;
            CurrentBar         = 0;
        });
    }

    private static async Task<AudioConfiguration?> TryLoadAudioConfigAsync()
    {
        if (!File.Exists(AppPathService.AudioConfigPath))
            return null;

        try
        {
            return await AudioConfiguration.LoadAsync(AppPathService.AudioConfigPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Live] Config load failed: {ex.Message}");
            return null;
        }
    }

    // ── Logging helpers ───────────────────────────────────────────────────

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        Debug.WriteLine(line);

        lock (_logLock)
        {
            _logBuffer.AppendLine(line);
        }

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            lock (_logLock)
            {
                LogOutput = _logBuffer.ToString();
            }
        });
    }

    public void Dispose()
    {
        _ = StopInternalAsync();
    }
}
