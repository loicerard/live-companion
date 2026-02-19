using System.Windows;
using LiveCompanion.App.Services;
using LiveCompanion.App.ViewModels;
using LiveCompanion.Audio;
using LiveCompanion.Midi;

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

        // Composition root
        _services = new AppServices(audioConfig, midiConfig);

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
}
