using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;
using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;
using Microsoft.Win32;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// ViewModel for the Config (setlist editor) view.
/// Exposes the setlist hierarchy and per-AudioCue Browse / Preview actions (Bug 2).
/// </summary>
public partial class ConfigViewModel : ObservableObject
{
    private Setlist _setlist = new();

    // ── Bindable properties ──────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<SongRowViewModel> _songs = [];

    [ObservableProperty]
    private SongRowViewModel? _selectedSong;

    [ObservableProperty]
    private SectionRowViewModel? _selectedSection;

    [ObservableProperty]
    private string _setlistName = "Nouvelle setlist";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ── Constructor ──────────────────────────────────────────────────────

    public ConfigViewModel()
    {
        _ = TryAutoLoadAsync();
    }

    // ── Commands ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenSetlistAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title  = "Ouvrir une setlist",
            Filter = "Setlist JSON|*.json|Tous les fichiers|*.*",
            InitialDirectory = AppPathService.SetlistsDirectory,
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _setlist = await SetlistRepository.LoadAsync(dialog.FileName);
            Refresh();
            StatusMessage = $"Setlist chargée : {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur de chargement :\n{ex.Message}", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SaveSetlistAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title            = "Sauvegarder la setlist",
            Filter           = "Setlist JSON|*.json",
            InitialDirectory = AppPathService.SetlistsDirectory,
            FileName         = _setlist.Name + ".json",
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _setlist.Name = SetlistName;
            Directory.CreateDirectory(AppPathService.SetlistsDirectory);
            await SetlistRepository.SaveAsync(_setlist, dialog.FileName);
            StatusMessage = $"Setlist sauvegardée : {dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur de sauvegarde :\n{ex.Message}", "Erreur",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Expose current setlist to other ViewModels ───────────────────────

    /// <summary>Returns the current in-memory setlist (used by LiveViewModel).</summary>
    public Setlist GetSetlist() => _setlist;

    // ── Private helpers ──────────────────────────────────────────────────

    private void Refresh()
    {
        SetlistName = _setlist.Name;
        var rows = _setlist.Songs.Select(s => new SongRowViewModel(s)).ToList();
        Songs = new ObservableCollection<SongRowViewModel>(rows);
        SelectedSong    = null;
        SelectedSection = null;
    }

    private async Task TryAutoLoadAsync()
    {
        // Auto-load the first setlist found in the AppData directory
        var dir = AppPathService.SetlistsDirectory;
        if (!Directory.Exists(dir))
            return;

        var first = Directory.GetFiles(dir, "*.json").FirstOrDefault();
        if (first is null)
            return;

        try
        {
            _setlist = await SetlistRepository.LoadAsync(first);
            Refresh();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Config] Auto-load failed: {ex.Message}");
        }
    }
}

// ── Row ViewModels ────────────────────────────────────────────────────────

public partial class SongRowViewModel : ObservableObject
{
    public Song Model { get; }

    public string Title  => Model.Title;
    public string Artist => Model.Artist;

    public ObservableCollection<SectionRowViewModel> Sections { get; }

    public SongRowViewModel(Song model)
    {
        Model    = model;
        Sections = new ObservableCollection<SectionRowViewModel>(
            model.Events
                 .OfType<SectionChangeEvent>()
                 .OrderBy(e => e.Tick)
                 .Select(s => new SectionRowViewModel(s, model)));
    }
}

public partial class SectionRowViewModel : ObservableObject
{
    public SectionChangeEvent Model { get; }

    public string SectionName => Model.SectionName;
    public int    Bpm         => Model.Bpm;

    /// <summary>AudioCue ViewModels for this section, shown in the edit panel.</summary>
    public ObservableCollection<AudioCueViewModel> AudioCues { get; }

    public SectionRowViewModel(SectionChangeEvent model, Song song)
    {
        Model = model;

        // Find AudioCues in the parent song that belong after this section's
        // tick and before the next section's tick.
        var nextSectionTick = song.Events
            .OfType<SectionChangeEvent>()
            .Where(s => s.Tick > model.Tick)
            .OrderBy(s => s.Tick)
            .FirstOrDefault()?.Tick ?? long.MaxValue;

        AudioCues = new ObservableCollection<AudioCueViewModel>(
            song.Events
                .OfType<AudioCueEvent>()
                .Where(c => c.Tick >= model.Tick && c.Tick < nextSectionTick)
                .OrderBy(c => c.Tick)
                .Select(c => new AudioCueViewModel(c)));
    }
}
