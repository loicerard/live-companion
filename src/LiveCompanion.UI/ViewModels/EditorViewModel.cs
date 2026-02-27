using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.UI.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    private readonly IProjectStore _projectStore;

    // ------------------------------------------------------------------ //
    // Bibliothèque de morceaux (panneau gauche)
    // ------------------------------------------------------------------ //

    public ObservableCollection<Song> Songs { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSongCommand))]
    private Song? _selectedSong;

    [ObservableProperty]
    private string _newSongTitle = string.Empty;

    // ------------------------------------------------------------------ //
    // Édition de sections (panneau droit)
    // ------------------------------------------------------------------ //

    public ObservableCollection<SectionViewModel> Sections { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSectionUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSectionDownCommand))]
    private SectionViewModel? _selectedSection;

    /// <summary>Numérateurs disponibles pour la signature rythmique.</summary>
    public IReadOnlyList<int> AvailableNumerators { get; } = [2, 3, 4, 5, 6, 7, 9, 12];

    /// <summary>Dénominateurs disponibles pour la signature rythmique.</summary>
    public IReadOnlyList<int> AvailableDenominators { get; } = [2, 4, 8];

    [ObservableProperty]
    private string? _statusMessage;

    // ------------------------------------------------------------------ //
    // Constructeur
    // ------------------------------------------------------------------ //

    public EditorViewModel(IProjectStore projectStore)
    {
        _projectStore = projectStore;
        RefreshSongList();
    }

    private void RefreshSongList()
    {
        Songs.Clear();
        foreach (var song in _projectStore.GetAll())
            Songs.Add(song);
    }

    // ------------------------------------------------------------------ //
    // Changement de sélection du morceau
    // ------------------------------------------------------------------ //

    partial void OnSelectedSongChanged(Song? value)
    {
        LoadSectionsFromSong(value);
    }

    private void LoadSectionsFromSong(Song? song)
    {
        Sections.Clear();
        SelectedSection = null;

        if (song is null) return;

        foreach (var section in song.Sections.OrderBy(s => s.Order))
            Sections.Add(new SectionViewModel(section));

        if (Sections.Count > 0)
            SelectedSection = Sections[0];
    }

    // ------------------------------------------------------------------ //
    // Commandes — Morceaux
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void CreateSong()
    {
        var title = string.IsNullOrWhiteSpace(NewSongTitle)
            ? "Nouveau morceau"
            : NewSongTitle.Trim();

        var song = _projectStore.CreateNew(title);
        Songs.Add(song);
        SelectedSong = song;
        NewSongTitle = string.Empty;
        StatusMessage = $"Morceau \"{song.Title}\" créé.";
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSong))]
    private void DeleteSong()
    {
        if (SelectedSong is null) return;

        var title = SelectedSong.Title;
        _projectStore.Delete(SelectedSong.Id);
        Songs.Remove(SelectedSong);
        SelectedSong = Songs.FirstOrDefault();
        StatusMessage = $"Morceau \"{title}\" supprimé.";
    }

    private bool CanDeleteSong() => SelectedSong is not null;

    // ------------------------------------------------------------------ //
    // Commandes — Sections
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void AddSection()
    {
        if (SelectedSong is null) return;

        var newSection = new Section
        {
            Name = $"Section {Sections.Count + 1}",
            Tempo = 120,
            TimeSignature = TimeSignature.Default,
            BarCount = 4,
            Order = Sections.Count,
        };

        SelectedSong.Sections.Add(newSection);
        var vm = new SectionViewModel(newSection);
        Sections.Add(vm);
        SelectedSection = vm;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSection))]
    private void RemoveSection()
    {
        if (SelectedSong is null || SelectedSection is null) return;

        var model = SelectedSection.Model;
        SelectedSong.Sections.Remove(model);
        Sections.Remove(SelectedSection);
        ReorderSections();
        SelectedSection = Sections.LastOrDefault();
    }

    private bool CanRemoveSection() => SelectedSection is not null;

    [RelayCommand(CanExecute = nameof(CanMoveSectionUp))]
    private void MoveSectionUp()
    {
        if (SelectedSection is null) return;
        var index = Sections.IndexOf(SelectedSection);
        if (index <= 0) return;

        Sections.Move(index, index - 1);
        ReorderSections();
        MoveSectionUpCommand.NotifyCanExecuteChanged();
        MoveSectionDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveSectionUp()
        => SelectedSection is not null && Sections.IndexOf(SelectedSection) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveSectionDown))]
    private void MoveSectionDown()
    {
        if (SelectedSection is null) return;
        var index = Sections.IndexOf(SelectedSection);
        if (index < 0 || index >= Sections.Count - 1) return;

        Sections.Move(index, index + 1);
        ReorderSections();
        MoveSectionUpCommand.NotifyCanExecuteChanged();
        MoveSectionDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveSectionDown()
        => SelectedSection is not null
        && Sections.IndexOf(SelectedSection) < Sections.Count - 1;

    private void ReorderSections()
    {
        for (int i = 0; i < Sections.Count; i++)
            Sections[i].Model.Order = i;
    }

    // ------------------------------------------------------------------ //
    // Sauvegarde
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void SaveSong()
    {
        if (SelectedSong is null) return;

        bool hasErrors = false;
        foreach (var section in Sections)
        {
            section.Validate();
            if (section.HasErrors)
                hasErrors = true;
        }

        if (hasErrors)
        {
            StatusMessage = "Corrigez les erreurs de validation avant de sauvegarder.";
            return;
        }

        foreach (var section in Sections)
            section.ApplyToModel();

        SelectedSong.LastModified = DateTime.UtcNow;
        StatusMessage = $"Morceau \"{SelectedSong.Title}\" sauvegardé.";
    }
}
