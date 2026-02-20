using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;
using LiveCompanion.Audio;
using LiveCompanion.Audio.Abstractions;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// ViewModel for the Setup view.
/// Handles ASIO driver selection, volume configuration, and the Test Beat feature.
/// </summary>
public partial class SetupViewModel : ObservableObject
{
    // ── Bug 1: Test Beat state ───────────────────────────────────────────
    // The Test Beat must produce short discrete clicks (≤20 ms bursts from
    // MetronomeWaveProvider), play exactly 4 beats, then stop automatically.
    private const int TestBeatCount = 4;

    private AsioService? _testAsioService;
    private MetronomeAudioEngine? _testMetronome;

    // ── Bindable properties ──────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<string> _availableDrivers = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestBeatCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveConfigCommand))]
    private string _selectedDriver = string.Empty;

    [ObservableProperty]
    private int _sampleRate = AudioConfiguration.DefaultSampleRate;

    [ObservableProperty]
    private int _bufferSize = AudioConfiguration.DefaultBufferSize;

    [ObservableProperty]
    private int _testBpm = 120;

    [ObservableProperty]
    private float _masterVolume = AudioConfiguration.DefaultMasterVolume;

    [ObservableProperty]
    private float _strongBeatVolume = AudioConfiguration.DefaultStrongBeatVolume;

    [ObservableProperty]
    private float _weakBeatVolume = AudioConfiguration.DefaultWeakBeatVolume;

    [ObservableProperty]
    private int _metronomeChannelOffset = AudioConfiguration.DefaultMetronomeChannel;

    [ObservableProperty]
    private int _sampleChannelOffset = AudioConfiguration.DefaultSampleChannel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestBeatCommand))]
    private bool _isTestBeatRunning;

    [ObservableProperty]
    private string _statusMessage = "Prêt.";

    // ── Constructor ──────────────────────────────────────────────────────

    public SetupViewModel()
    {
        LoadAvailableDrivers();
        _ = TryLoadConfigAsync();
    }

    // ── Commands ─────────────────────────────────────────────────────────

    /// <summary>
    /// Bug 1 — Test Beat:
    /// Plays exactly <see cref="TestBeatCount"/> beats via ASIO and then stops
    /// automatically. The click bursts are 15 ms (strong) / 10 ms (weak) as
    /// fixed in <see cref="LiveCompanion.Audio.Providers.MetronomeWaveProvider"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTestBeat))]
    private async Task TestBeatAsync()
    {
        IsTestBeatRunning = true;
        StatusMessage = $"Test Beat — 0 / {TestBeatCount} beats…";

        try
        {
            var config = BuildConfig();

            // Tear down any leftover test session
            StopTestSession();

            _testAsioService = new AsioService(new NAudioAsioOutFactory(), config);
            _testAsioService.Initialize();

            // Bug 3 diagnostic: confirm ASIO initialized
            Debug.WriteLine("[TestBeat] ASIO initialized successfully.");

            _testMetronome = new MetronomeAudioEngine(
                _testAsioService, config, ppqn: 480, initialBpm: TestBpm);

            int beatsHeard = 0;
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _testMetronome.Beat += (beat, bar) =>
            {
                beatsHeard++;
                Debug.WriteLine($"[TestBeat] Beat {beatsHeard}/{TestBeatCount} (beat={beat} bar={bar})");

                Application.Current.Dispatcher.InvokeAsync(() =>
                    StatusMessage = $"Test Beat — {beatsHeard} / {TestBeatCount} beats…");

                if (beatsHeard >= TestBeatCount)
                {
                    // Stop from within the ASIO callback — MetronomeWaveProvider
                    // will output silence on the very next buffer.
                    _testMetronome?.Stop();
                    done.TrySetResult();
                }
            };

            _testMetronome.Start();
            _testAsioService.Play();
            Debug.WriteLine("[TestBeat] ASIO Play() called — callback running.");

            // Wait for 4 beats (or a 10 s safety timeout)
            await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TestBeat] Error: {ex}");
            StatusMessage = $"Erreur Test Beat : {ex.Message}";
        }
        finally
        {
            StopTestSession();
            IsTestBeatRunning = false;
            StatusMessage = "Test Beat terminé.";
        }
    }

    private bool CanTestBeat() => !string.IsNullOrEmpty(SelectedDriver) && !IsTestBeatRunning;

    [RelayCommand(CanExecute = nameof(CanSaveConfig))]
    private async Task SaveConfigAsync()
    {
        try
        {
            var config = BuildConfig();
            await AudioConfiguration.SaveAsync(config, AppPathService.AudioConfigPath);
            StatusMessage = "Configuration sauvegardée.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur sauvegarde : {ex.Message}";
        }
    }

    private bool CanSaveConfig() => !string.IsNullOrEmpty(SelectedDriver);

    // ── Helpers ──────────────────────────────────────────────────────────

    private void LoadAvailableDrivers()
    {
        try
        {
            // Use a temporary factory just to enumerate drivers; no ASIO init yet.
            var factory = new NAudioAsioOutFactory();
            var drivers = factory.GetDriverNames();
            AvailableDrivers = new ObservableCollection<string>(drivers);

            if (AvailableDrivers.Count > 0)
                SelectedDriver = AvailableDrivers[0];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Setup] Driver enumeration failed: {ex.Message}");
            StatusMessage = $"Impossible de lister les drivers ASIO : {ex.Message}";
        }
    }

    private async Task TryLoadConfigAsync()
    {
        if (!File.Exists(AppPathService.AudioConfigPath))
            return;

        try
        {
            var config = await AudioConfiguration.LoadAsync(AppPathService.AudioConfigPath);
            SelectedDriver         = config.AsioDriverName ?? SelectedDriver;
            SampleRate             = config.SampleRate;
            BufferSize             = config.BufferSize;
            MasterVolume           = config.MetronomeMasterVolume;
            StrongBeatVolume       = config.StrongBeatVolume;
            WeakBeatVolume         = config.WeakBeatVolume;
            MetronomeChannelOffset = config.MetronomeChannelOffset;
            SampleChannelOffset    = config.SampleChannelOffset;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Setup] Config load failed: {ex.Message}");
        }
    }

    private AudioConfiguration BuildConfig() => new()
    {
        AsioDriverName         = SelectedDriver,
        SampleRate             = SampleRate,
        BufferSize             = BufferSize,
        MetronomeMasterVolume  = MasterVolume,
        StrongBeatVolume       = StrongBeatVolume,
        WeakBeatVolume         = WeakBeatVolume,
        MetronomeChannelOffset = MetronomeChannelOffset,
        SampleChannelOffset    = SampleChannelOffset,
        AutoReconnect          = true,
        ReconnectDelayMs       = 2000,
    };

    private void StopTestSession()
    {
        try { _testMetronome?.Stop(); } catch { /* ignore */ }
        try { _testAsioService?.Stop(); } catch { /* ignore */ }
        try { _testAsioService?.Dispose(); } catch { /* ignore */ }
        _testMetronome = null;
        _testAsioService = null;
    }
}
