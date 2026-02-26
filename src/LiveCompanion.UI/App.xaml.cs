using System.Windows;
using LiveCompanion.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LiveCompanion.UI;

public partial class App : Application
{
    /// <summary>
    /// Mode moteur actuel. Changer en <see cref="EngineMode.Real"/> quand les moteurs réels seront prêts.
    /// </summary>
    private const EngineMode CurrentEngineMode = EngineMode.Mock;

    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddEngines(CurrentEngineMode);
        services.AddViewModels();
    }
}
