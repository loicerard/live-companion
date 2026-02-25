using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LiveCompanion.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentView;

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

    public MainViewModel()
    {
        _currentView = new LiveViewModel();
    }

    [RelayCommand]
    private void Navigate(string destination)
    {
        CurrentView = destination switch
        {
            "Live" => new LiveViewModel(),
            "Editor" => new EditorViewModel(),
            "Library" => new LibraryViewModel(),
            "Config" => new ConfigViewModel(),
            _ => CurrentView
        };
        ActiveSection = destination;
    }
}
