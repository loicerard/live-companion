using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;
using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// ViewModel for the live performance view.
/// Subscribes to SetlistPlayer and MetronomeAudioEngine events and exposes
/// everything the stage display needs.
/// </summary>
public sealed partial class PerformanceViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private readonly IDispatcher _dispatcher;
    private readonly INavigationService _nav;
    private bool _subscribed;

    public PerformanceViewModel(AppServices services, IDispatcher dispatcher,
                                INavigationService nav)
    {
        _services   = services;
        _dispatcher = dispatcher;
        _nav        = nav;
        Subscribe();
    }

    // ── Song info ───────────────────────────────────────────────────

    [ObservableProperty]
    private string _songTitle = "—";

    [ObservableProperty]
    private string _sectionName = string.Empty;

    [ObservableProperty]
    private int _bpm = 120;

    [ObservableProperty]
    private string _timeSignature = "4/4";

    // ── Song navigation info ────────────────────────────────────────

    [ObservableProperty]
    private int _currentSongNumber;

    [ObservableProperty]
    private int _totalSongs;

    // ── Beat indicator ─────────────────────────────────────────────

    /// <summary>
    /// True for one UI cycle when a strong beat fires (beat 0 in the bar).
    /// </summary>
    [ObservableProperty]
    private bool _isStrongBeat;

    /// <summary>
    /// True for one UI cycle when any beat fires.
    /// </summary>
    [ObservableProperty]
    private bool _isBeatActive;

    // ── Player state ───────────────────────────────────────────────

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _canPlay;

    [ObservableProperty]
    private bool _canStop;

    [ObservableProperty]
    private bool _canGoNext;

    [ObservableProperty]
    private bool _canGoPrevious;

    // ── Commands ───────────────────────────────────────────────────

    [RelayCommand]
    private void Play()
    {
        if (_services.Player.State == PlayerState.Playing) return;

        if (_services.Player.State == PlayerState.Idle ||
            _services.Player.State == PlayerState.Stopped)
        {
            _services.Player.Play();
            _services.MetronomeAudio?.Start();
            _services.MidiClock?.Start();
        }

        UpdatePlaybackState();
    }

    [RelayCommand]
    private void Stop()
    {
        _services.Player.Stop();
        _services.MetronomeAudio?.Stop();
        _services.MidiClock?.Stop();
        UpdatePlaybackState();
    }

    [RelayCommand]
    private void GoToPerformance() => _nav.NavigateTo(ViewKey.Performance);

    // ── Subscriptions ──────────────────────────────────────────────

    private void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;

        _services.Player.SongStarted    += OnSongStarted;
        _services.Player.SongEnded      += OnSongEnded;
        _services.Player.BeatFired      += OnBeatFired;
        _services.Player.SectionChanged += OnSectionChanged;
        _services.Player.SetlistCompleted += OnSetlistCompleted;

        UpdatePlaybackState();
        UpdateSongInfo();
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;

        _services.Player.SongStarted    -= OnSongStarted;
        _services.Player.SongEnded      -= OnSongEnded;
        _services.Player.BeatFired      -= OnBeatFired;
        _services.Player.SectionChanged -= OnSectionChanged;
        _services.Player.SetlistCompleted -= OnSetlistCompleted;
    }

    // ── Event handlers ─────────────────────────────────────────────

    private void OnSongStarted(Song song, int index)
    {
        _dispatcher.Post(() =>
        {
            SongTitle         = song.Title;
            CurrentSongNumber = index + 1;
            SectionName       = string.Empty;
            UpdatePlaybackState();
        });
    }

    private void OnSongEnded(Song song, int index)
    {
        _dispatcher.Post(UpdatePlaybackState);
    }

    private void OnBeatFired(int beat, int bar)
    {
        bool isStrong = beat == 0;
        _dispatcher.Post(() =>
        {
            IsStrongBeat = isStrong;
            IsBeatActive = true;
        });

        // Flash duration: ~100 ms — reset after a short delay
        _ = Task.Delay(120).ContinueWith(_ =>
            _dispatcher.Post(() =>
            {
                IsBeatActive = false;
                IsStrongBeat = false;
            }), TaskScheduler.Default);
    }

    private void OnSectionChanged(SectionChangeEvent section)
    {
        _dispatcher.Post(() =>
        {
            Bpm           = section.Bpm;
            TimeSignature = $"{section.TimeSignature.Numerator}/{section.TimeSignature.Denominator}";
            SectionName   = section.SectionName;
        });
    }

    private void OnSetlistCompleted()
    {
        _dispatcher.Post(() =>
        {
            IsPlaying = false;
            UpdatePlaybackState();
        });
    }

    // ── Helpers ────────────────────────────────────────────────────

    private void UpdatePlaybackState()
    {
        var state = _services.Player.State;
        IsPlaying   = state == PlayerState.Playing;
        CanPlay     = state != PlayerState.Playing;
        CanStop     = state == PlayerState.Playing;
        CanGoNext   = state != PlayerState.Playing;
        CanGoPrevious = state != PlayerState.Playing;
    }

    private void UpdateSongInfo()
    {
        var song = _services.Player.CurrentSong;
        if (song is null) return;
        SongTitle         = song.Title;
        CurrentSongNumber = _services.Player.CurrentSongIndex + 1;
    }

    public void Dispose()
    {
        Unsubscribe();
    }
}
