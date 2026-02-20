using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;
using LiveCompanion.Core.Engine;
using LiveCompanion.Core.Models;
using Microsoft.Win32;
using NAudio.Wave;

namespace LiveCompanion.App.ViewModels;

// ── Tree node view models ──────────────────────────────────────────

public sealed partial class AudioCueItemViewModel : ObservableObject, IDisposable
{
    private WaveOutEvent? _previewOut;
    private AudioFileReader? _previewReader;

    public AudioCueItemViewModel(AudioCueEvent cue)
    {
        _tick     = (int)cue.Tick;
        _fileName = cue.SampleFileName;
        _gainDb   = cue.GainDb;
    }

    [ObservableProperty] private int    _tick;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    private string _fileName = string.Empty;

    [ObservableProperty] private double _gainDb;

    public AudioCueEvent ToModel() => new()
    {
        Tick           = Tick,
        SampleFileName = FileName,
        GainDb         = GainDb,
    };

    // ── Bug 2 — Browse ────────────────────────────────────────────
    /// <summary>
    /// Opens an OpenFileDialog filtered to audio formats, copies the chosen
    /// file into %AppData%\LiveCompanion\samples\ and stores only the
    /// relative file name so the setlist stays portable.
    /// </summary>
    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title           = "Sélectionner un fichier audio",
            Filter          = "Fichiers audio|*.mp3;*.wav;*.ogg;*.flac|Tous les fichiers|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var destDir  = ConfigPaths.SamplesDirectory;
            Directory.CreateDirectory(destDir);

            var fileName = Path.GetFileName(dialog.FileName);
            var destPath = Path.Combine(destDir, fileName);
            File.Copy(dialog.FileName, destPath, overwrite: true);

            // Store only the relative name — SamplePlayer resolves it at load time
            FileName = fileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de copier le fichier :\n{ex.Message}",
                            "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Bug 2 — Preview ───────────────────────────────────────────
    /// <summary>
    /// Plays the sample through the default Windows audio output (WaveOutEvent).
    /// Does NOT require ASIO to be initialized.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void Preview()
    {
        StopPreview();

        var fullPath = Path.Combine(ConfigPaths.SamplesDirectory, FileName);
        if (!File.Exists(fullPath))
        {
            MessageBox.Show($"Fichier introuvable :\n{fullPath}",
                            "Aperçu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _previewReader = new AudioFileReader(fullPath);
            _previewOut    = new WaveOutEvent();
            _previewOut.Init(_previewReader);
            _previewOut.PlaybackStopped += (_, _) => StopPreview();
            _previewOut.Play();
        }
        catch (Exception ex)
        {
            StopPreview();
            MessageBox.Show($"Erreur lecture :\n{ex.Message}",
                            "Aperçu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanPreview() => !string.IsNullOrWhiteSpace(FileName);

    private void StopPreview()
    {
        _previewOut?.Stop();
        _previewOut?.Dispose();
        _previewReader?.Dispose();
        _previewOut    = null;
        _previewReader = null;
    }

    public void Dispose() => StopPreview();
}

public sealed partial class MidiPresetItemViewModel : ObservableObject
{
    public MidiPresetItemViewModel(MidiPreset preset)
    {
        _device        = preset.Device;
        _channel       = preset.Channel;
        _programChange = preset.ProgramChange;
        CcList = new ObservableCollection<string>(
            preset.ControlChanges.Select(cc => $"CC{cc.Controller}={cc.Value}"));
    }

    [ObservableProperty] private DeviceTarget _device;
    [ObservableProperty] private int          _channel;
    [ObservableProperty] private int          _programChange;

    public ObservableCollection<string> CcList { get; }

    public MidiPreset ToModel() => new()
    {
        Device        = Device,
        Channel       = Channel,
        ProgramChange = ProgramChange,
        ControlChanges = CcList
            .Select(s => ParseCc(s))
            .Where(cc => cc is not null)
            .Select(cc => cc!)
            .ToList(),
    };

    private static ControlChange? ParseCc(string s)
    {
        // Format: "CCnn=vv"
        if (!s.StartsWith("CC", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = s[2..].Split('=');
        if (parts.Length != 2) return null;
        if (int.TryParse(parts[0], out int ctrl) && int.TryParse(parts[1], out int val))
            return new ControlChange(ctrl, val);
        return null;
    }
}

public sealed partial class SectionNodeViewModel : ObservableObject
{
    public SectionNodeViewModel(SectionChangeEvent evt)
    {
        _sectionName  = evt.SectionName;
        _bpm          = evt.Bpm;
        _timeSigNum   = evt.TimeSignature.Numerator;
        _timeSigDen   = evt.TimeSignature.Denominator;
        _tick         = (int)evt.Tick;

        Presets  = new ObservableCollection<MidiPresetItemViewModel>(
            evt.Presets.Select(p => new MidiPresetItemViewModel(p)));
        AudioCues = [];
    }

    [ObservableProperty] private string _sectionName = string.Empty;
    [ObservableProperty] private int    _bpm;
    [ObservableProperty] private int    _timeSigNum;
    [ObservableProperty] private int    _timeSigDen;
    [ObservableProperty] private int    _tick;

    public ObservableCollection<MidiPresetItemViewModel> Presets   { get; }
    public ObservableCollection<AudioCueItemViewModel>   AudioCues { get; }

    public string DisplayName => $"[{Tick}] {SectionName} — {Bpm} BPM";

    public SectionChangeEvent ToModel() => new()
    {
        Tick          = Tick,
        SectionName   = SectionName,
        Bpm           = Bpm,
        TimeSignature = new TimeSignature(TimeSigNum, TimeSigDen),
        Presets       = Presets.Select(p => p.ToModel()).ToList(),
    };
}

public sealed partial class SongNodeViewModel : ObservableObject
{
    public SongNodeViewModel(Song song)
    {
        _title         = song.Title;
        _artist        = song.Artist;
        _durationTicks = (int)song.DurationTicks;

        Sections  = new ObservableCollection<SectionNodeViewModel>(
            song.Events.OfType<SectionChangeEvent>()
                       .OrderBy(e => e.Tick)
                       .Select(e => new SectionNodeViewModel(e)));
        AudioCues = new ObservableCollection<AudioCueItemViewModel>(
            song.Events.OfType<AudioCueEvent>()
                       .OrderBy(e => e.Tick)
                       .Select(e => new AudioCueItemViewModel(e)));
    }

    [ObservableProperty] private string _title         = string.Empty;
    [ObservableProperty] private string _artist        = string.Empty;
    [ObservableProperty] private int    _durationTicks;

    public ObservableCollection<SectionNodeViewModel>  Sections  { get; }
    public ObservableCollection<AudioCueItemViewModel> AudioCues { get; }

    public string DisplayName => string.IsNullOrEmpty(Artist) ? Title : $"{Title} — {Artist}";

    public Song ToModel()
    {
        var events = new List<SongEvent>();
        events.AddRange(Sections.Select(s => s.ToModel()));
        events.AddRange(AudioCues.Select(c => c.ToModel()));
        return new Song
        {
            Title         = Title,
            Artist        = Artist,
            DurationTicks = DurationTicks,
            Events        = events,
        };
    }
}

// ── ConfigViewModel ───────────────────────────────────────────────

/// <summary>
/// ViewModel for the setlist / song / section editor.
/// </summary>
public sealed partial class ConfigViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly INavigationService _nav;
    private readonly NotificationViewModel _notification;
    private readonly SetlistViewModel _setlistVm;

    private string? _currentFilePath;

    public ConfigViewModel(AppServices services, INavigationService nav,
                           NotificationViewModel notification, SetlistViewModel setlistVm)
    {
        _services     = services;
        _nav          = nav;
        _notification = notification;
        _setlistVm    = setlistVm;
    }

    // ── Setlist root ───────────────────────────────────────────────

    [ObservableProperty]
    private string _setlistName = string.Empty;

    public ObservableCollection<SongNodeViewModel> Songs { get; } = [];

    // ── Selection ─────────────────────────────────────────────────

    [ObservableProperty]
    private SongNodeViewModel? _selectedSong;

    [ObservableProperty]
    private SectionNodeViewModel? _selectedSection;

    [ObservableProperty]
    private object? _selectedNode;

    partial void OnSelectedNodeChanged(object? value)
    {
        SelectedSong    = value as SongNodeViewModel;
        SelectedSection = value as SectionNodeViewModel;
    }

    // ── Commands ───────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadSetlist()
    {
        var dialog = new OpenFileDialog
        {
            Title  = "Open Setlist",
            Filter = "JSON Setlist (*.json)|*.json|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var setlist = await SetlistRepository.LoadAsync(dialog.FileName);
            _currentFilePath = dialog.FileName;
            LoadSetlistIntoTree(setlist);
        }
        catch (Exception ex)
        {
            _notification.ShowError($"Failed to load setlist: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveSetlist()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            await SaveSetlistAs();
            return;
        }

        await SaveToFile(_currentFilePath);
    }

    [RelayCommand]
    private async Task SaveSetlistAs()
    {
        var dialog = new SaveFileDialog
        {
            Title            = "Save Setlist",
            Filter           = "JSON Setlist (*.json)|*.json",
            DefaultExt       = ".json",
            FileName         = SetlistName,
        };
        if (dialog.ShowDialog() != true) return;

        _currentFilePath = dialog.FileName;
        await SaveToFile(_currentFilePath);
    }

    [RelayCommand]
    private void AddSong()
    {
        var song = new Song { Title = "New Song", DurationTicks = 7680 };
        Songs.Add(new SongNodeViewModel(song));
    }

    [RelayCommand]
    private void RemoveSong(SongNodeViewModel? node)
    {
        if (node is null) return;
        Songs.Remove(node);
        if (SelectedSong == node) SelectedNode = null;
    }

    [RelayCommand]
    private void AddSection(SongNodeViewModel? song)
    {
        song ??= SelectedSong;
        if (song is null) return;

        var section = new SectionChangeEvent
        {
            Tick          = 0,
            SectionName   = "New Section",
            Bpm           = 120,
            TimeSignature = TimeSignature.Common,
        };
        song.Sections.Add(new SectionNodeViewModel(section));
    }

    [RelayCommand]
    private void RemoveSection(SectionNodeViewModel? node)
    {
        if (node is null || SelectedSong is null) return;
        SelectedSong.Sections.Remove(node);
        if (SelectedSection == node) SelectedNode = null;
    }

    // ── Private helpers ────────────────────────────────────────────

    private void LoadSetlistIntoTree(Setlist setlist)
    {
        SetlistName = setlist.Name;
        Songs.Clear();
        foreach (var song in setlist.Songs)
            Songs.Add(new SongNodeViewModel(song));
    }

    private async Task SaveToFile(string filePath)
    {
        try
        {
            var setlist = BuildSetlistFromTree();
            await SetlistRepository.SaveAsync(setlist, filePath);
            _setlistVm.NotifySetlistChanged(setlist);
            _setlistVm.NotifySetlistSaved(filePath);
            _notification.ShowWarning("Setlist saved.");
        }
        catch (Exception ex)
        {
            _notification.ShowError($"Failed to save setlist: {ex.Message}");
        }
    }

    private Setlist BuildSetlistFromTree()
    {
        return new Setlist
        {
            Name  = SetlistName,
            Songs = Songs.Select(s => s.ToModel()).ToList(),
        };
    }
}
