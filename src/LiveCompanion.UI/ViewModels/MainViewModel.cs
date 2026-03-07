using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LiveCompanion.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly Func<LiveViewModel> _liveFactory;
    private readonly Func<EditorViewModel> _editorFactory;
    private readonly Func<LibraryViewModel> _libraryFactory;
    private readonly Func<ConfigViewModel> _configFactory;

    [ObservableProperty]
    private ViewModelBase _currentView;

    /// <summary>
    /// True pendant l'initialisation asynchrone des moteurs audio/MIDI au démarrage.
    /// </summary>
    [ObservableProperty]
    private bool _isInitializing;

    [ObservableProperty]
    private string _activeSection = "Live";

    partial void OnActiveSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsLiveActive));
        OnPropertyChanged(nameof(IsEditorActive));
        OnPropertyChanged(nameof(IsLibraryActive));
        OnPropertyChanged(nameof(IsConfigActive));
    }

    public bool IsLiveActive => _activeSection == "Live";
    public bool IsEditorActive => _activeSection == "Editor";
    public bool IsLibraryActive => _activeSection == "Library";
    public bool IsConfigActive => _activeSection == "Config";

    public MainViewModel(
        Func<LiveViewModel> liveFactory,
        Func<EditorViewModel> editorFactory,
        Func<LibraryViewModel> libraryFactory,
        Func<ConfigViewModel> configFactory)
    {
        _liveFactory = liveFactory;
        _editorFactory = editorFactory;
        _libraryFactory = libraryFactory;
        _configFactory = configFactory;

        _currentView = _liveFactory();
    }

    [RelayCommand]
    private void Navigate(string destination)
    {
        var newView = destination switch
        {
            "Live" => (ViewModelBase)_liveFactory(),
            "Editor" => _editorFactory(),
            "Library" => _libraryFactory(),
            "Config" => _configFactory(),
            _ => CurrentView
        };

        if (newView != CurrentView)
        {
            (CurrentView as IDisposable)?.Dispose();
            CurrentView = newView;
        }

        ActiveSection = destination;
    }
}
