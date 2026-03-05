using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;
using Microsoft.Win32;

namespace LiveCompanion.UI.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    private readonly IProjectStore _projectStore;
    private readonly IMidiEngine _midiEngine;
    private readonly ILogService _log;
    private readonly ILiveModeGuard _liveModeGuard;

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
    // Édition de sections
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

    // ------------------------------------------------------------------ //
    // Samples audio (#18)
    // ------------------------------------------------------------------ //

    public ObservableCollection<AudioClipViewModel> AudioClips { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveAudioClipCommand))]
    private AudioClipViewModel? _selectedAudioClip;

    /// <summary>Modes de synchronisation pour le ComboBox.</summary>
    public IReadOnlyList<SyncMode> AvailableSyncModes { get; } = Enum.GetValues<SyncMode>();

    // ------------------------------------------------------------------ //
    // Événements MIDI (#19)
    // ------------------------------------------------------------------ //

    public ObservableCollection<MidiEventViewModel> MidiEvents { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveMidiEventCommand))]
    private MidiEventViewModel? _selectedMidiEvent;

    /// <summary>Types d'événements MIDI pour le ComboBox.</summary>
    public IReadOnlyList<MidiEventType> AvailableMidiEventTypes { get; } = Enum.GetValues<MidiEventType>();

    /// <summary>Ports MIDI de sortie disponibles (mock).</summary>
    public IReadOnlyList<string> AvailableMidiPorts => _midiEngine.GetAvailablePorts();

    // ------------------------------------------------------------------ //
    // Piste de clic (#20)
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private string? _clickTrackPath;

    [ObservableProperty]
    private string? _clickTrackFileName;

    // ------------------------------------------------------------------ //
    // Statut
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private string? _statusMessage;

    // ------------------------------------------------------------------ //
    // Constructeur
    // ------------------------------------------------------------------ //

    public EditorViewModel(IProjectStore projectStore, IMidiEngine midiEngine, ILogService log, ILiveModeGuard liveModeGuard)
    {
        _projectStore = projectStore;
        _midiEngine = midiEngine;
        _log = log;
        _liveModeGuard = liveModeGuard;
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
        LoadSongData(value);
    }

    private void LoadSongData(Song? song)
    {
        Sections.Clear();
        SelectedSection = null;
        AudioClips.Clear();
        SelectedAudioClip = null;
        MidiEvents.Clear();
        SelectedMidiEvent = null;

        if (song is null)
        {
            ClickTrackPath = null;
            ClickTrackFileName = null;
            return;
        }

        // Sections
        foreach (var section in song.Sections.OrderBy(s => s.Order))
            Sections.Add(new SectionViewModel(section));

        if (Sections.Count > 0)
            SelectedSection = Sections[0];

        // Samples
        foreach (var clip in song.AudioClips)
            AudioClips.Add(new AudioClipViewModel(clip));

        if (AudioClips.Count > 0)
            SelectedAudioClip = AudioClips[0];

        // MIDI
        foreach (var evt in song.MidiEvents)
            MidiEvents.Add(new MidiEventViewModel(evt));

        if (MidiEvents.Count > 0)
            SelectedMidiEvent = MidiEvents[0];

        // Clic
        ClickTrackPath = song.ClickTrackPath;
        ClickTrackFileName = string.IsNullOrEmpty(song.ClickTrackPath)
            ? null
            : System.IO.Path.GetFileName(song.ClickTrackPath);
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
        if (_liveModeGuard.IsLive)
        {
            StatusMessage = "Action non disponible en mode Live.";
            return;
        }

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
        if (_liveModeGuard.IsLive)
        {
            StatusMessage = "Action non disponible en mode Live.";
            return;
        }

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
    // Commandes — Samples (#18)
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void AddAudioClip()
    {
        if (SelectedSong is null) return;

        var clip = new AudioClip
        {
            Name = $"Sample {AudioClips.Count + 1}",
            BusName = "Main",
            Volume = 1.0,
            SyncMode = SyncMode.Free,
            Position = TimelinePosition.Zero,
        };

        SelectedSong.AudioClips.Add(clip);
        var vm = new AudioClipViewModel(clip);
        AudioClips.Add(vm);
        SelectedAudioClip = vm;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveAudioClip))]
    private void RemoveAudioClip()
    {
        if (SelectedSong is null || SelectedAudioClip is null) return;
        if (_liveModeGuard.IsLive)
        {
            StatusMessage = "Action non disponible en mode Live.";
            return;
        }

        SelectedSong.AudioClips.Remove(SelectedAudioClip.Model);
        AudioClips.Remove(SelectedAudioClip);
        SelectedAudioClip = AudioClips.LastOrDefault();
    }

    private bool CanRemoveAudioClip() => SelectedAudioClip is not null;

    [RelayCommand]
    private void BrowseAudioClipFile()
    {
        if (SelectedAudioClip is null) return;

        var dialog = new OpenFileDialog
        {
            Title = "Sélectionner un fichier audio",
            Filter = "Fichiers audio|*.wav;*.mp3;*.flac;*.aiff;*.aif|Tous|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedAudioClip.FilePath = dialog.FileName;
            if (string.IsNullOrEmpty(SelectedAudioClip.Name) ||
                SelectedAudioClip.Name.StartsWith("Sample "))
            {
                SelectedAudioClip.Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    // ------------------------------------------------------------------ //
    // Commandes — Événements MIDI (#19)
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void AddMidiEvent()
    {
        if (SelectedSong is null) return;

        var evt = new MidiEvent
        {
            Type = MidiEventType.ProgramChange,
            DeviceOut = AvailableMidiPorts.FirstOrDefault() ?? string.Empty,
            Channel = 1,
            Position = TimelinePosition.Zero,
        };

        SelectedSong.MidiEvents.Add(evt);
        var vm = new MidiEventViewModel(evt);
        MidiEvents.Add(vm);
        SelectedMidiEvent = vm;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveMidiEvent))]
    private void RemoveMidiEvent()
    {
        if (SelectedSong is null || SelectedMidiEvent is null) return;
        if (_liveModeGuard.IsLive)
        {
            StatusMessage = "Action non disponible en mode Live.";
            return;
        }

        SelectedSong.MidiEvents.Remove(SelectedMidiEvent.Model);
        MidiEvents.Remove(SelectedMidiEvent);
        SelectedMidiEvent = MidiEvents.LastOrDefault();
    }

    private bool CanRemoveMidiEvent() => SelectedMidiEvent is not null;

    [RelayCommand]
    private async Task TestMidiEvent()
    {
        if (SelectedMidiEvent is null) return;

        SelectedMidiEvent.ApplyToModel();

        try
        {
            await _midiEngine.SendEventAsync(SelectedMidiEvent.Model);
            _log.Debug(LogSource.UI, $"[EditorVM] Test MIDI → {SelectedMidiEvent.DisplaySummary}");
            StatusMessage = $"Test MIDI envoyé : {SelectedMidiEvent.DisplaySummary}";
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "Initialisez le MIDI dans Configuration avant de tester.";
        }
    }

    // ------------------------------------------------------------------ //
    // Commandes — Piste de clic (#20)
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void BrowseClickTrack()
    {
        if (SelectedSong is null) return;

        var dialog = new OpenFileDialog
        {
            Title = "Sélectionner la piste de clic",
            Filter = "Fichiers audio|*.wav;*.mp3;*.flac;*.aiff;*.aif|Tous|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedSong.ClickTrackPath = dialog.FileName;
            ClickTrackPath = dialog.FileName;
            ClickTrackFileName = System.IO.Path.GetFileName(dialog.FileName);
            StatusMessage = $"Piste de clic assignée : {ClickTrackFileName}";
        }
    }

    [RelayCommand]
    private void ClearClickTrack()
    {
        if (SelectedSong is null) return;
        if (_liveModeGuard.IsLive)
        {
            StatusMessage = "Action non disponible en mode Live.";
            return;
        }

        SelectedSong.ClickTrackPath = null;
        ClickTrackPath = null;
        ClickTrackFileName = null;
        StatusMessage = "Piste de clic supprimée.";
    }

    // ------------------------------------------------------------------ //
    // Sauvegarde
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void SaveSong()
    {
        if (SelectedSong is null) return;

        // Valider sections
        bool hasErrors = false;
        foreach (var section in Sections)
        {
            section.Validate();
            if (section.HasErrors)
                hasErrors = true;
        }

        // Valider samples
        foreach (var clip in AudioClips)
        {
            clip.Validate();
            if (clip.HasErrors)
                hasErrors = true;
        }

        // Valider MIDI
        foreach (var evt in MidiEvents)
        {
            evt.Validate();
            if (evt.HasErrors)
                hasErrors = true;
        }

        if (hasErrors)
        {
            StatusMessage = "Corrigez les erreurs de validation avant de sauvegarder.";
            return;
        }

        foreach (var section in Sections)
            section.ApplyToModel();

        foreach (var clip in AudioClips)
            clip.ApplyToModel();

        foreach (var evt in MidiEvents)
            evt.ApplyToModel();

        _projectStore.Update(SelectedSong);
        StatusMessage = $"Morceau \"{SelectedSong.Title}\" sauvegardé.";
    }
}
