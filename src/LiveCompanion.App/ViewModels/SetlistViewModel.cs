using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;
using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;
using Microsoft.Win32;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// ViewModel for the setlist preparation / navigation view.
/// </summary>
public sealed partial class SetlistViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDispatcher _dispatcher;
    private readonly INavigationService _nav;
    private readonly NotificationViewModel _notification;

    public SetlistViewModel(AppServices services, IDispatcher dispatcher,
                            INavigationService nav, NotificationViewModel notification)
    {
        _services     = services;
        _dispatcher   = dispatcher;
        _nav          = nav;
        _notification = notification;

        _services.Player.SongStarted += OnSongStarted;
    }

    public ObservableCollection<SongItemViewModel> Songs { get; } = [];

    [ObservableProperty]
    private SongItemViewModel? _selectedSong;

    [ObservableProperty]
    private string _setlistName = string.Empty;

    [ObservableProperty]
    private bool _hasSetlist;

    private Setlist? _currentSetlist;

    // ── Commands ───────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadSetlist()
    {
        var dialog = new OpenFileDialog
        {
            Title            = "Open Setlist",
            Filter           = "JSON Setlist (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists  = true,
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var setlist = await SetlistRepository.LoadAsync(dialog.FileName);
            LoadSetlistIntoUi(setlist);
        }
        catch (Exception ex)
        {
            _notification.ShowError($"Failed to load setlist: {ex.Message}");
        }
    }

    [RelayCommand]
    private void NewSetlist()
    {
        var setlist = new Setlist { Name = "New Setlist" };
        LoadSetlistIntoUi(setlist);
        _nav.NavigateTo(ViewKey.Config);
    }

    [RelayCommand]
    private void GoToPerformance()
    {
        _nav.NavigateTo(ViewKey.Performance);
    }

    [RelayCommand]
    private void OpenSong(SongItemViewModel? item)
    {
        if (item is null) return;
        if (_services.Player.State == PlayerState.Playing) return;

        // Jump to song index in the setlist
        SelectedSong = item;
        _nav.NavigateTo(ViewKey.Performance);
    }

    // ── Private helpers ────────────────────────────────────────────

    private void LoadSetlistIntoUi(Setlist setlist)
    {
        _currentSetlist = setlist;
        SetlistName     = setlist.Name;

        if (_services.Player.State != PlayerState.Playing)
        {
            _services.Player.Load(setlist);
        }

        RefreshSongs(setlist);
        HasSetlist = true;
    }

    private void RefreshSongs(Setlist setlist)
    {
        Songs.Clear();
        for (int i = 0; i < setlist.Songs.Count; i++)
        {
            Songs.Add(new SongItemViewModel(setlist.Songs[i], i, setlist.Ppqn));
        }
        UpdateCurrentSongHighlight();
    }

    private void OnSongStarted(Song song, int index)
    {
        _dispatcher.Post(UpdateCurrentSongHighlight);
    }

    private void UpdateCurrentSongHighlight()
    {
        int current = _services.Player.CurrentSongIndex;
        foreach (var item in Songs)
            item.IsCurrent = item.Index == current;
    }

    public void NotifySetlistChanged(Setlist setlist)
    {
        _currentSetlist = setlist;
        SetlistName = setlist.Name;
        RefreshSongs(setlist);
    }
}
