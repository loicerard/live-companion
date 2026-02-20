using System.IO;
using System.Windows;
using LiveCompanion.App.Services;

namespace LiveCompanion.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Ensure the AppData directories exist at startup
        Directory.CreateDirectory(AppPathService.SamplesDirectory);
        Directory.CreateDirectory(AppPathService.SetlistsDirectory);
    }
}
