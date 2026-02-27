using System.Collections.ObjectModel;
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
    private List<Song> _songs = [];
    private int _songIndex;

    // ------------------------------------------------------------------ //
    // Propriétés observables
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousSongCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextSongCommand))]
    private string _songTitle = "--";

    [ObservableProperty]
    private string _songPositionDisplay = "0 / 0";

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
    [NotifyCanExecuteChangedFor(nameof(PreviousSongCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextSongCommand))]
    private TransportState _currentState = TransportState.Stopped;

    // ------------------------------------------------------------------ //
    // Timeline (#17)
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private IReadOnlyList<Section>? _timelineSections;

    [ObservableProperty]
    private int _timelineSectionIndex;

    [ObservableProperty]
    private int _timelineBar = 1;

    [ObservableProperty]
    private int _timelineBeat = 1;

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

        RefreshSongList();
    }

    private void RefreshSongList()
    {
        _songs = _projectStore.GetAll().ToList();

        if (_songs.Count == 0)
        {
            // Créer un morceau par défaut si le store est vide
            var defaultSong = _projectStore.CreateNew();
            _songs = _projectStore.GetAll().ToList();
        }

        _songIndex = 0;
        LoadSong(_songs[0]);
    }

    private void LoadSong(Song song)
    {
        _song = song;
        SongTitle = song.Title;
        SongPositionDisplay = $"{_songIndex + 1} / {_songs.Count}";
        TimelineSections = song.Sections.AsReadOnly();
        UpdateSectionDisplay(0);
        ResetPosition();
    }

    private void ResetPosition()
    {
        PositionDisplay = "1 : 1";
        TimelineSectionIndex = 0;
        TimelineBar = 1;
        TimelineBeat = 1;
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

        ResetPosition();
    }

    [RelayCommand(CanExecute = nameof(CanTransition))]
    private async Task NextSectionAsync()
    {
        await _scheduler.NextSectionAsync();
    }

    // ------------------------------------------------------------------ //
    // Commandes navigation morceaux
    // ------------------------------------------------------------------ //

    [RelayCommand(CanExecute = nameof(CanPreviousSong))]
    private async Task PreviousSongAsync()
    {
        if (_currentState != TransportState.Stopped)
        {
            await _scheduler.StopAsync();
            await _transport.StopAsync();
        }

        _songIndex--;
        LoadSong(_songs[_songIndex]);
    }

    private bool CanPreviousSong()
        => _currentState == TransportState.Stopped && _songIndex > 0;

    [RelayCommand(CanExecute = nameof(CanNextSong))]
    private async Task NextSongAsync()
    {
        if (_currentState != TransportState.Stopped)
        {
            await _scheduler.StopAsync();
            await _transport.StopAsync();
        }

        _songIndex++;
        LoadSong(_songs[_songIndex]);
    }

    private bool CanNextSong()
        => _currentState == TransportState.Stopped && _songIndex < _songs.Count - 1;

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
            TimelineSectionIndex = position.SectionIndex;
            TimelineBar = position.Bar;
            TimelineBeat = position.Beat;
        });
    }

    private void OnSectionChanged(object? sender, int sectionIndex)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateSectionDisplay(sectionIndex);
            TimelineSectionIndex = sectionIndex;
        });
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
