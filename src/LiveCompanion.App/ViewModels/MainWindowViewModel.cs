using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// Shell view model: owns navigation state and the notification overlay.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    private readonly AppServices _services;

    public MainWindowViewModel(INavigationService nav, AppServices services,
                               NotificationViewModel notification,
                               PerformanceViewModel performance,
                               SetlistViewModel setlist,
                               ConfigViewModel config,
                               SetupViewModel setup)
    {
        _nav          = nav;
        _services     = services;
        Notification  = notification;
        Performance   = performance;
        Setlist       = setlist;
        Config        = config;
        Setup         = setup;

        // Wire fault notifications
        _services.AsioService.AudioFault  += msg => Notification.ShowError($"Audio fault: {msg}");
        _services.MidiService.MidiFault   += (port, ex) => Notification.ShowError($"MIDI fault on '{port}': {ex.Message}");

        // Hide nav bar only while actively playing (not just when on the Performance view)
        Performance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PerformanceViewModel.IsPlaying))
                RefreshPerformanceMode();
        };

        // Track navigation changes
        _nav.NavigatedTo += OnNavigatedTo;
        OnNavigatedTo(_nav.CurrentView);
    }

    // ── Child view models ──────────────────────────────────────────

    public NotificationViewModel Notification { get; }
    public PerformanceViewModel  Performance  { get; }
    public SetlistViewModel      Setlist      { get; }
    public ConfigViewModel       Config       { get; }
    public SetupViewModel        Setup        { get; }

    // ── Navigation state ───────────────────────────────────────────

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private bool _isPerformanceMode;

    [ObservableProperty]
    private bool _isNavPerformanceActive;

    [ObservableProperty]
    private bool _isNavSetlistActive;

    [ObservableProperty]
    private bool _isNavConfigActive;

    [ObservableProperty]
    private bool _isNavSetupActive;

    // ── Nav commands ───────────────────────────────────────────────

    [RelayCommand]
    private void GoPerformance() => _nav.NavigateTo(ViewKey.Performance);

    [RelayCommand]
    private void GoSetlist() => _nav.NavigateTo(ViewKey.Setlist);

    [RelayCommand]
    private void GoConfig() => _nav.NavigateTo(ViewKey.Config);

    [RelayCommand]
    private void GoSetup() => _nav.NavigateTo(ViewKey.Setup);

    // ── Private ────────────────────────────────────────────────────

    private void OnNavigatedTo(ViewKey view)
    {
        CurrentView = view switch
        {
            ViewKey.Performance => Performance,
            ViewKey.Setlist     => Setlist,
            ViewKey.Config      => Config,
            ViewKey.Setup       => Setup,
            _                   => Setlist,
        };

        IsNavPerformanceActive   = view == ViewKey.Performance;
        IsNavSetlistActive       = view == ViewKey.Setlist;
        IsNavConfigActive        = view == ViewKey.Config;
        IsNavSetupActive         = view == ViewKey.Setup;
        RefreshPerformanceMode();
    }

    /// <summary>
    /// Nav bar is hidden only when on the Performance view AND the player is actively playing.
    /// In Idle/Stopped state the sidebar stays visible so the user can navigate away.
    /// </summary>
    private void RefreshPerformanceMode()
    {
        IsPerformanceMode = _nav.CurrentView == ViewKey.Performance && Performance.IsPlaying;
    }
}
