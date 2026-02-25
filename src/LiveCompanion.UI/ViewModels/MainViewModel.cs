using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LiveCompanion.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentView;

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
    }
}
