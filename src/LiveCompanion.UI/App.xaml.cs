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

        // Démarrer la sauvegarde automatique
        var autoSave = Services.GetRequiredService<IAutoSaveService>();
        autoSave.Start();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
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
