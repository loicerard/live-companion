using System.IO;
using System.Diagnostics;
using System.Windows;
using LiveCompanion.App.Services;
using LiveCompanion.App.ViewModels;
using LiveCompanion.Audio;
using LiveCompanion.Audio.Abstractions;
using LiveCompanion.Midi;
using Microsoft.Extensions.Logging;

namespace LiveCompanion.App;

public partial class App : Application
{
    private AppServices? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load persisted configurations (best-effort)
        var audioConfig = await TryLoadAudioConfigAsync();
        var midiConfig  = await TryLoadMidiConfigAsync();

        // Composition root — pass a factory with a debug logger so ASIO driver
        // enumeration details appear in the VS Output window (Debug pane).
        var asioFactory = new NAudioAsioOutFactory(new AsioDebugLogger());
        _services = new AppServices(audioConfig, midiConfig, asioFactory);

        var dispatcher  = new WpfDispatcher();
        var navService  = new NavigationService();

        // Try to initialize ASIO if a driver is configured
        bool asioReady = false;
        if (!string.IsNullOrEmpty(audioConfig.AsioDriverName))
        {
            try
            {
                _services.InitializeAudio();
                asioReady = true;
            }
            catch
            {
                // ASIO init failure is non-fatal; user will be redirected to Setup
            }
        }

        // Build ViewModels
        var notification = new NotificationViewModel(dispatcher);
        var performance  = new PerformanceViewModel(_services, dispatcher, navService);
        var setlist      = new SetlistViewModel(_services, dispatcher, navService, notification);
        var config       = new ConfigViewModel(_services, navService, notification, setlist);
        var setup        = new SetupViewModel(_services, navService, notification);

        var mainVm = new MainWindowViewModel(navService, _services, notification,
                                             performance, setlist, config, setup);

        var window = new MainWindow(mainVm);
        window.Closed += (_, _) => OnWindowClosed();
        window.Show();

        // Navigate: go to Setup if no audio/MIDI config exists yet
        bool hasConfig = !string.IsNullOrEmpty(audioConfig.AsioDriverName) && asioReady;
        navService.NavigateTo(hasConfig ? ViewKey.Setlist : ViewKey.Setup);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private void OnWindowClosed()
    {
        _services?.Dispose();
        Shutdown();
    }

    private static async Task<AudioConfiguration> TryLoadAudioConfigAsync()
    {
        try
        {
            if (File.Exists(ConfigPaths.AudioConfigFile))
                return await AudioConfiguration.LoadAsync(ConfigPaths.AudioConfigFile);
        }
        catch { /* use defaults */ }

        return new AudioConfiguration();
    }

    private static async Task<MidiConfiguration> TryLoadMidiConfigAsync()
    {
        try
        {
            if (File.Exists(ConfigPaths.MidiConfigFile))
            {
                var json = await File.ReadAllTextAsync(ConfigPaths.MidiConfigFile);
                return System.Text.Json.JsonSerializer.Deserialize<MidiConfiguration>(
                    json,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
                    })
                    ?? new MidiConfiguration();
            }
        }
        catch { /* use defaults */ }

        return new MidiConfiguration();
    }

    // ── Simple logger that forwards to System.Diagnostics.Debug ────────────
    // Visible in Visual Studio: View → Output → Debug.
    // No extra NuGet package needed — ILogger<T> is already in
    // Microsoft.Extensions.Logging.Abstractions which the App project references.

    private sealed class AsioDebugLogger : ILogger<NAudioAsioOutFactory>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                                Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = $"[ASIO][{logLevel}] {formatter(state, exception)}";
            Debug.WriteLine(msg);
            if (exception is not null)
                Debug.WriteLine($"[ASIO][Exception] {exception.GetType().Name}: {exception.Message}");
        }
    }
}
