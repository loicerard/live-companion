using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using Microsoft.Win32;

namespace LiveCompanion.UI.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly IProjectStore _projectStore;
    private readonly List<Song> _allSongs = [];

    // ------------------------------------------------------------------ //
    // Songs
    // ------------------------------------------------------------------ //

    public ObservableCollection<Song> FilteredSongs { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSongCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddToPlaylistCommand))]
    private Song? _selectedSong;

    [ObservableProperty]
    private string _newSongTitle = string.Empty;

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    partial void OnSearchFilterChanged(string value) => ApplyFilter();

    // ------------------------------------------------------------------ //
    // Playlists
    // ------------------------------------------------------------------ //

    public ObservableCollection<Playlist> Playlists { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeletePlaylistCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddToPlaylistCommand))]
    private Playlist? _selectedPlaylist;

    [ObservableProperty]
    private string _newPlaylistName = string.Empty;

    // ------------------------------------------------------------------ //
    // Contenu de la playlist sélectionnée
    // ------------------------------------------------------------------ //

    public ObservableCollection<Song> PlaylistSongs { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveFromPlaylistCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpInPlaylistCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownInPlaylistCommand))]
    private Song? _selectedPlaylistSong;

    // ------------------------------------------------------------------ //
    // Statut
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private string? _statusMessage;

    // ------------------------------------------------------------------ //
    // Constructeur
    // ------------------------------------------------------------------ //

    public LibraryViewModel(IProjectStore projectStore)
    {
        _projectStore = projectStore;
        RefreshSongList();
        RefreshPlaylistList();
    }

    // ------------------------------------------------------------------ //
    // Commandes — Songs
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void CreateSong()
    {
        var title = string.IsNullOrWhiteSpace(NewSongTitle)
            ? "Nouveau morceau"
            : NewSongTitle.Trim();

        var song = _projectStore.CreateNew(title);
        _allSongs.Add(song);
        ApplyFilter();
        SelectedSong = song;
        NewSongTitle = string.Empty;
        StatusMessage = $"Morceau \"{song.Title}\" créé.";
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSong))]
    private void DeleteSong()
    {
        if (SelectedSong is null) return;

        var title = SelectedSong.Title;
        var songId = SelectedSong.Id;

        _projectStore.Delete(songId);
        _allSongs.RemoveAll(s => s.Id == songId);
        ApplyFilter();

        // Retirer des playlists qui référencent ce morceau
        foreach (var playlist in Playlists)
        {
            if (playlist.SongIds.Remove(songId))
                _projectStore.UpdatePlaylist(playlist);
        }

        SelectedSong = FilteredSongs.FirstOrDefault();
        RefreshPlaylistSongs();
        StatusMessage = $"Morceau \"{title}\" supprimé.";
    }

    private bool CanDeleteSong() => SelectedSong is not null;

    // ------------------------------------------------------------------ //
    // Commandes — Playlists
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void CreatePlaylist()
    {
        var name = string.IsNullOrWhiteSpace(NewPlaylistName)
            ? "Playlist"
            : NewPlaylistName.Trim();

        var playlist = _projectStore.CreatePlaylist(name);
        Playlists.Add(playlist);
        SelectedPlaylist = playlist;
        NewPlaylistName = string.Empty;
        StatusMessage = $"Playlist \"{playlist.Name}\" créée.";
    }

    [RelayCommand(CanExecute = nameof(CanDeletePlaylist))]
    private void DeletePlaylist()
    {
        if (SelectedPlaylist is null) return;

        var name = SelectedPlaylist.Name;
        _projectStore.DeletePlaylist(SelectedPlaylist.Id);
        Playlists.Remove(SelectedPlaylist);
        SelectedPlaylist = Playlists.FirstOrDefault();
        StatusMessage = $"Playlist \"{name}\" supprimée.";
    }

    private bool CanDeletePlaylist() => SelectedPlaylist is not null;

    [RelayCommand(CanExecute = nameof(CanAddToPlaylist))]
    private void AddToPlaylist()
    {
        if (SelectedSong is null || SelectedPlaylist is null) return;

        if (SelectedPlaylist.SongIds.Contains(SelectedSong.Id))
        {
            StatusMessage = $"\"{SelectedSong.Title}\" est déjà dans la playlist.";
            return;
        }

        SelectedPlaylist.SongIds.Add(SelectedSong.Id);
        _projectStore.UpdatePlaylist(SelectedPlaylist);
        RefreshPlaylistSongs();
        StatusMessage = $"\"{SelectedSong.Title}\" ajouté à \"{SelectedPlaylist.Name}\".";
    }

    private bool CanAddToPlaylist() => SelectedSong is not null && SelectedPlaylist is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveFromPlaylist))]
    private void RemoveFromPlaylist()
    {
        if (SelectedPlaylistSong is null || SelectedPlaylist is null) return;

        var title = SelectedPlaylistSong.Title;
        SelectedPlaylist.SongIds.Remove(SelectedPlaylistSong.Id);
        _projectStore.UpdatePlaylist(SelectedPlaylist);
        RefreshPlaylistSongs();
        StatusMessage = $"\"{title}\" retiré de \"{SelectedPlaylist.Name}\".";
    }

    private bool CanRemoveFromPlaylist() => SelectedPlaylistSong is not null && SelectedPlaylist is not null;

    [RelayCommand(CanExecute = nameof(CanMoveUpInPlaylist))]
    private void MoveUpInPlaylist()
    {
        if (SelectedPlaylistSong is null || SelectedPlaylist is null) return;

        var index = SelectedPlaylist.SongIds.IndexOf(SelectedPlaylistSong.Id);
        if (index <= 0) return;

        (SelectedPlaylist.SongIds[index], SelectedPlaylist.SongIds[index - 1]) =
            (SelectedPlaylist.SongIds[index - 1], SelectedPlaylist.SongIds[index]);

        _projectStore.UpdatePlaylist(SelectedPlaylist);
        var song = SelectedPlaylistSong;
        RefreshPlaylistSongs();
        SelectedPlaylistSong = song;
    }

    private bool CanMoveUpInPlaylist()
        => SelectedPlaylistSong is not null
        && SelectedPlaylist is not null
        && SelectedPlaylist.SongIds.IndexOf(SelectedPlaylistSong.Id) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveDownInPlaylist))]
    private void MoveDownInPlaylist()
    {
        if (SelectedPlaylistSong is null || SelectedPlaylist is null) return;

        var index = SelectedPlaylist.SongIds.IndexOf(SelectedPlaylistSong.Id);
        if (index < 0 || index >= SelectedPlaylist.SongIds.Count - 1) return;

        (SelectedPlaylist.SongIds[index], SelectedPlaylist.SongIds[index + 1]) =
            (SelectedPlaylist.SongIds[index + 1], SelectedPlaylist.SongIds[index]);

        _projectStore.UpdatePlaylist(SelectedPlaylist);
        var song = SelectedPlaylistSong;
        RefreshPlaylistSongs();
        SelectedPlaylistSong = song;
    }

    private bool CanMoveDownInPlaylist()
        => SelectedPlaylistSong is not null
        && SelectedPlaylist is not null
        && SelectedPlaylist.SongIds.IndexOf(SelectedPlaylistSong.Id) < SelectedPlaylist.SongIds.Count - 1;

    // ------------------------------------------------------------------ //
    // Changement de sélection
    // ------------------------------------------------------------------ //

    partial void OnSelectedPlaylistChanged(Playlist? value)
    {
        RefreshPlaylistSongs();
    }

    // ------------------------------------------------------------------ //
    // Méthodes privées
    // ------------------------------------------------------------------ //

    private void RefreshSongList()
    {
        _allSongs.Clear();
        _allSongs.AddRange(_projectStore.GetAll());
        ApplyFilter();
    }

    private void RefreshPlaylistList()
    {
        Playlists.Clear();
        foreach (var playlist in _projectStore.GetAllPlaylists())
            Playlists.Add(playlist);
    }

    private void RefreshPlaylistSongs()
    {
        PlaylistSongs.Clear();
        SelectedPlaylistSong = null;

        if (SelectedPlaylist is null) return;

        var songMap = _allSongs.ToDictionary(s => s.Id);
        foreach (var songId in SelectedPlaylist.SongIds)
        {
            if (songMap.TryGetValue(songId, out var song))
                PlaylistSongs.Add(song);
        }
    }

    private void ApplyFilter()
    {
        FilteredSongs.Clear();

        var filter = SearchFilter?.Trim() ?? string.Empty;
        var songs = string.IsNullOrEmpty(filter)
            ? _allSongs
            : _allSongs.Where(s => s.Title.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var song in songs.OrderBy(s => s.Title))
            FilteredSongs.Add(song);
    }

    // ------------------------------------------------------------------ //
    // Import / Export — Morceaux (disque)
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task ImportSongAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Charger un morceau",
            Filter = "Fichier JSON|*.json|Tous|*.*",
        };

        if (dialog.ShowDialog() != true) return;

        var result = await _projectStore.LoadAsync(dialog.FileName);
        if (!result.Validation.IsValid)
        {
            StatusMessage = $"Erreur : {string.Join(", ", result.Validation.Issues.Select(i => i.Message))}";
            return;
        }

        _allSongs.Add(result.Value!);
        ApplyFilter();
        SelectedSong = result.Value;
        StatusMessage = $"Morceau \"{result.Value!.Title}\" importé depuis {System.IO.Path.GetFileName(dialog.FileName)}";
    }

    [RelayCommand]
    private async Task ExportSongAsync()
    {
        if (SelectedSong is null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Exporter le morceau",
            Filter = "Fichier JSON|*.json",
            FileName = $"{SelectedSong.Title}.json",
        };

        if (dialog.ShowDialog() != true) return;

        var result = await _projectStore.SaveAsync(SelectedSong, dialog.FileName);
        StatusMessage = result.IsValid
            ? $"Morceau \"{SelectedSong.Title}\" exporté → {dialog.FileName}"
            : $"Erreur : {string.Join(", ", result.Issues.Select(i => i.Message))}";
    }

    // ------------------------------------------------------------------ //
    // Import / Export — Tous les morceaux (disque)
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task ExportAllSongsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exporter tous les morceaux",
            Filter = "Fichier JSON|*.json",
            FileName = "morceaux.json",
        };

        if (dialog.ShowDialog() != true) return;

        var result = await _projectStore.SaveAllSongsAsync(dialog.FileName);
        StatusMessage = result.IsValid
            ? $"{_allSongs.Count} morceau(x) exporté(s) → {dialog.FileName}"
            : $"Erreur : {string.Join(", ", result.Issues.Select(i => i.Message))}";
    }

    [RelayCommand]
    private async Task ImportAllSongsAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importer des morceaux",
            Filter = "Fichier JSON|*.json|Tous|*.*",
        };

        if (dialog.ShowDialog() != true) return;

        var result = await _projectStore.LoadAllSongsAsync(dialog.FileName);
        if (!result.Validation.IsValid)
        {
            StatusMessage = $"Erreur : {string.Join(", ", result.Validation.Issues.Select(i => i.Message))}";
            return;
        }

        // Écraser tous les morceaux existants
        _allSongs.Clear();
        foreach (var song in result.Value!)
            _allSongs.Add(song);

        ApplyFilter();
        if (FilteredSongs.Count > 0)
            SelectedSong = FilteredSongs[0];

        StatusMessage = $"{result.Value!.Count} morceau(x) importé(s) depuis {System.IO.Path.GetFileName(dialog.FileName)}";
    }

    // ------------------------------------------------------------------ //
    // Import / Export — Playlists (disque)
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task ExportPlaylistsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exporter les playlists",
            Filter = "Fichier JSON|*.json",
            FileName = "playlists.json",
        };

        if (dialog.ShowDialog() != true) return;

        var result = await _projectStore.SavePlaylistsAsync(dialog.FileName);
        StatusMessage = result.IsValid
            ? $"Playlists exportées → {dialog.FileName}"
            : $"Erreur : {string.Join(", ", result.Issues.Select(i => i.Message))}";
    }

    [RelayCommand]
    private async Task ImportPlaylistsAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importer des playlists",
            Filter = "Fichier JSON|*.json|Tous|*.*",
        };

        if (dialog.ShowDialog() != true) return;

        var result = await _projectStore.LoadPlaylistsAsync(dialog.FileName);
        if (!result.Validation.IsValid)
        {
            StatusMessage = $"Erreur : {string.Join(", ", result.Validation.Issues.Select(i => i.Message))}";
            return;
        }

        // Ajouter les playlists importées (sans doublons par Id)
        var existingIds = Playlists.Select(p => p.Id).ToHashSet();
        var imported = 0;
        foreach (var playlist in result.Value!)
        {
            if (!existingIds.Contains(playlist.Id))
            {
                Playlists.Add(playlist);
                imported++;
            }
        }

        if (imported > 0)
            SelectedPlaylist = Playlists.Last();

        StatusMessage = $"{imported} playlist(s) importée(s) depuis {System.IO.Path.GetFileName(dialog.FileName)}";
    }
}
