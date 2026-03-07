using System.IO;
using System.Windows;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.UI.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LiveCompanion.UI;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddCommandLine(e.Args)
            .Build();

        var engineMode = ResolveEngineMode(configuration);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IConfiguration>(configuration);
        serviceCollection.AddEngines(engineMode);
        serviceCollection.AddViewModels();
        Services = serviceCollection.BuildServiceProvider();

        var log = Services.GetRequiredService<ILogService>();
        log.Info(LogSource.UI, $"Live Companion started — EngineMode={engineMode}");

        // Charger les morceaux et playlists sauvegardés
        // Task.Run évite le deadlock WPF : sans lui, le SynchronizationContext
        // tente de reprendre les await sur le thread UI, qui est bloqué par GetResult().
        Task.Run(() => LoadPersistedData(Services)).GetAwaiter().GetResult();

        // Démarrer la sauvegarde automatique
        var autoSave = Services.GetRequiredService<IAutoSaveService>();
        autoSave.Start();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Sauvegarde finale avant fermeture
        var autoSave = Services.GetRequiredService<IAutoSaveService>();
        Task.Run(() => autoSave.SaveNowAsync()).GetAwaiter().GetResult();
        autoSave.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// Charge les morceaux individuels et les playlists depuis le dossier de sauvegarde.
    /// </summary>
    private static async Task LoadPersistedData(IServiceProvider sp)
    {
        var store = sp.GetRequiredService<IProjectStore>();
        var log = sp.GetRequiredService<ILogService>();
        var savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LiveCompanion",
            "songs");

        if (!Directory.Exists(savePath))
            return;

        // Charger chaque morceau sauvegardé individuellement
        var files = Directory.GetFiles(savePath, "*.json");
        var loaded = 0;
        foreach (var file in files)
        {
            if (Path.GetFileName(file).Equals("playlists.json", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var result = await store.LoadAsync(file);
                if (result.Validation.IsValid)
                    loaded++;
            }
            catch (Exception ex)
            {
                log.Warn(LogSource.UI, $"[Startup] Erreur chargement '{file}' — {ex.Message}");
            }
        }

        // Charger les playlists
        var playlistFile = Path.Combine(savePath, "playlists.json");
        if (File.Exists(playlistFile))
        {
            try
            {
                await store.LoadPlaylistsAsync(playlistFile);
            }
            catch (Exception ex)
            {
                log.Warn(LogSource.UI, $"[Startup] Erreur chargement playlists — {ex.Message}");
            }
        }

        if (loaded > 0)
            log.Info(LogSource.UI, $"[Startup] {loaded} morceau(x) restauré(s) depuis '{savePath}'");
    }

    /// <summary>
    /// Résout le mode moteur depuis la configuration (appsettings.json)
    /// avec possibilité d'override via l'argument CLI <c>--EngineMode=Real</c>.
    /// </summary>
    private static EngineMode ResolveEngineMode(IConfiguration configuration)
    {
        var value = configuration["EngineMode"];

        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<EngineMode>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return EngineMode.Mock;
    }
}
