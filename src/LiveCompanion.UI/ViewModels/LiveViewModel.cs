using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.UI.ViewModels;

public partial class LiveViewModel : ViewModelBase, IDisposable
{
    private readonly ITransportController _transport;
    private readonly ITimelineScheduler _scheduler;
    private readonly IProjectStore _projectStore;

    private Song? _song;
    private bool _disposed;

    // ------------------------------------------------------------------ //
    // Propriétés observables
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private string _currentSectionName = "--";

    [ObservableProperty]
    private string _nextSectionName = "--";

    [ObservableProperty]
    private double _tempo;

    [ObservableProperty]
    private string _timeSignatureDisplay = "4/4";

    [ObservableProperty]
    private string _positionDisplay = "1 : 1";

    [ObservableProperty]
    private TransportState _currentState = TransportState.Stopped;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextSectionCommand))]
    private bool _canTransition = true;

    public bool IsPlaying => _currentState == TransportState.Playing;
    public bool IsPaused => _currentState == TransportState.Paused;
    public bool IsStopped => _currentState == TransportState.Stopped;

    partial void OnCurrentStateChanged(TransportState value)
    {
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsStopped));
    }

    // ------------------------------------------------------------------ //
    // Constructeur
    // ------------------------------------------------------------------ //

    public LiveViewModel(
        ITransportController transport,
        ITimelineScheduler scheduler,
        IProjectStore projectStore)
    {
        _transport = transport;
        _scheduler = scheduler;
        _projectStore = projectStore;

        _transport.StateChanged += OnTransportStateChanged;
        _scheduler.PositionChanged += OnPositionChanged;
        _scheduler.SectionChanged += OnSectionChanged;

        LoadDefaultSong();
    }

    private void LoadDefaultSong()
    {
        _song = _projectStore.CreateNew();
        UpdateSectionDisplay(0);
    }

    private void UpdateSectionDisplay(int sectionIndex)
    {
        if (_song is null || sectionIndex >= _song.Sections.Count) return;

        var section = _song.Sections[sectionIndex];
        CurrentSectionName = section.Name;
        Tempo = section.Tempo;
        TimeSignatureDisplay = section.TimeSignature.ToString();

        NextSectionName = sectionIndex + 1 < _song.Sections.Count
            ? _song.Sections[sectionIndex + 1].Name
            : "--";
    }

    // ------------------------------------------------------------------ //
    // Commandes transport
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (_song is null) return;

        if (_currentState == TransportState.Stopped)
            await _scheduler.StartAsync(_song);

        await _transport.PlayAsync();
    }

    [RelayCommand]
    private async Task PauseAsync()
    {
        await _transport.PauseAsync();
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _scheduler.StopAsync();
        await _transport.StopAsync();

        if (_song is not null)
            UpdateSectionDisplay(0);

        PositionDisplay = "1 : 1";
    }

    [RelayCommand(CanExecute = nameof(CanTransition))]
    private async Task NextSectionAsync()
    {
        await _scheduler.NextSectionAsync();
    }

    // ------------------------------------------------------------------ //
    // Gestionnaires d'événements (dispatch UI thread)
    // ------------------------------------------------------------------ //

    private void OnTransportStateChanged(object? sender, TransportState state)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentState = state;
            CanTransition = _scheduler.CanTransitionNow;
        });
    }

    private void OnPositionChanged(object? sender, TimelinePosition position)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            PositionDisplay = $"{position.Bar} : {position.Beat}";
            CanTransition = _scheduler.CanTransitionNow;
        });
    }

    private void OnSectionChanged(object? sender, int sectionIndex)
    {
        Application.Current?.Dispatcher.Invoke(() => UpdateSectionDisplay(sectionIndex));
    }

    // ------------------------------------------------------------------ //
    // IDisposable
    // ------------------------------------------------------------------ //

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _transport.StateChanged -= OnTransportStateChanged;
        _scheduler.PositionChanged -= OnPositionChanged;
        _scheduler.SectionChanged -= OnSectionChanged;
    }
}
