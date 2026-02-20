namespace LiveCompanion.App.ViewModels;

/// <summary>
/// Root ViewModel for the application. Owns and wires the per-tab ViewModels.
/// </summary>
public sealed class MainViewModel : IDisposable
{
    public SetupViewModel  Setup  { get; }
    public ConfigViewModel Config { get; }
    public LiveViewModel   Live   { get; }

    public MainViewModel()
    {
        Setup  = new SetupViewModel();
        Config = new ConfigViewModel();
        Live   = new LiveViewModel(Config);
    }

    public void Dispose() => Live.Dispose();
}
