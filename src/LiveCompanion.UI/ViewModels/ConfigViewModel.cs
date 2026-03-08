using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Models;

namespace LiveCompanion.UI.ViewModels;

public partial class ConfigViewModel : ViewModelBase
{
    private readonly IAudioEngine _audioEngine;
    private readonly IMidiEngine _midiEngine;
    private readonly IProjectStore _store;

    // ------------------------------------------------------------------ //
    // Audio — Propriétés observables
    // ------------------------------------------------------------------ //

    public IReadOnlyList<string> AvailableDrivers { get; }

    public IReadOnlyList<int> AvailableBufferSizes { get; }

    /// <summary>
    /// Paires de sorties stéréo disponibles (ex: "Output 1-Output 2").
    /// Rafraîchies après application de la config audio.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<string> _availableOutputPairs = [];

    [ObservableProperty]
    private string? _selectedDriver;

    [ObservableProperty]
    private int _selectedBufferSize = 256;

    /// <summary>
    /// Bus mappings éditables : chaque entrée est un BusMapping (nom bus → sortie).
    /// </summary>
    public ObservableCollection<BusMapping> BusMappings { get; } = [];

    [ObservableProperty]
    private bool _audioInitialized;

    [ObservableProperty]
    private string? _audioStatusMessage;

    // ------------------------------------------------------------------ //
    // MIDI — Propriétés observables
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Ports MIDI disponibles avec leur état de sélection.
    /// </summary>
    public ObservableCollection<MidiPortItem> AvailableMidiPorts { get; } = [];

    [ObservableProperty]
    private string? _midiStatusMessage;

    [ObservableProperty]
    private string? _midiTestResult;

    public int SelectedMidiPortCount => AvailableMidiPorts.Count(p => p.IsSelected);

    // ------------------------------------------------------------------ //
    // Profils MIDI — Propriétés observables
    // ------------------------------------------------------------------ //

    /// <summary>Profils MIDI (un profil = un appareil avec ses raccourcis).</summary>
    public ObservableCollection<MidiProfile> MidiProfiles { get; } = [];

    [ObservableProperty]
    private MidiProfile? _selectedMidiProfile;

    /// <summary>Raccourcis du profil sélectionné.</summary>
    public ObservableCollection<MidiPreset> CurrentProfilePresets { get; } = [];

    [ObservableProperty]
    private MidiPreset? _selectedPreset;

    /// <summary>Noms des ports MIDI disponibles pour l'association au profil.</summary>
    public IReadOnlyList<string> AvailableMidiPortNames => _midiEngine.GetAvailablePorts();

    /// <summary>Types d'événements MIDI pour le ComboBox d'ajout de raccourci.</summary>
    public IReadOnlyList<MidiEventType> AvailableMidiEventTypes { get; } = Enum.GetValues<MidiEventType>();

    [ObservableProperty]
    private string _newPresetName = string.Empty;

    [ObservableProperty]
    private MidiEventType _newPresetType = MidiEventType.ControlChange;

    [ObservableProperty]
    private int _newPresetData1;

    [ObservableProperty]
    private int _newPresetData2;

    [ObservableProperty]
    private string? _profileStatusMessage;

    // ------------------------------------------------------------------ //
    // Constructeur
    // ------------------------------------------------------------------ //

    public ConfigViewModel(IAudioEngine audioEngine, IMidiEngine midiEngine, IProjectStore store)
    {
        _audioEngine = audioEngine;
        _midiEngine = midiEngine;
        _store = store;

        // Charger les listes depuis les moteurs
        AvailableDrivers = _audioEngine.GetAvailableDrivers();
        AvailableBufferSizes = _audioEngine.GetSupportedBufferSizes();

        if (AvailableDrivers.Count > 0)
            SelectedDriver = AvailableDrivers[0];

        // Charger les paires de sorties disponibles (peut être vide si pas de driver ouvert)
        AvailableOutputPairs = _audioEngine.GetAvailableOutputPairs();

        // Bus mappings par défaut
        var defaultOutput = AvailableOutputPairs.Count > 0 ? AvailableOutputPairs[0] : "Output 1-2";
        var clickOutput = AvailableOutputPairs.Count > 1 ? AvailableOutputPairs[1] : "Output 3-4";

        BusMappings.Add(new BusMapping { BusName = "Main", OutputName = defaultOutput });
        BusMappings.Add(new BusMapping { BusName = "Click", OutputName = clickOutput });
        BusMappings.Add(new BusMapping { BusName = "FX", OutputName = defaultOutput });

        // Ports MIDI
        var ports = _midiEngine.GetAvailablePorts();
        foreach (var port in ports)
        {
            var item = new MidiPortItem { PortName = port };
            item.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SelectedMidiPortCount));
            AvailableMidiPorts.Add(item);
        }

        // Charger les profils MIDI
        foreach (var profile in _store.GetSettings().MidiProfiles)
            MidiProfiles.Add(profile);

        // Restaurer la configuration sauvegardée
        RestoreSavedSettings();
    }

    partial void OnSelectedMidiProfileChanged(MidiProfile? value)
    {
        CurrentProfilePresets.Clear();
        SelectedPreset = null;

        if (value is null) return;

        foreach (var preset in value.Presets)
            CurrentProfilePresets.Add(preset);
    }

    partial void OnSelectedPresetChanged(MidiPreset? value)
    {
        if (value is null) return;

        // Pré-remplir le formulaire avec les valeurs du preset sélectionné
        NewPresetName = value.Name;
        NewPresetType = value.Type;
        NewPresetData1 = value.Data1;
        NewPresetData2 = value.Data2;
    }

    // ------------------------------------------------------------------ //
    // Restauration de la configuration sauvegardée
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Restaure les valeurs UI depuis les settings persistés (sans initialiser les moteurs).
    /// </summary>
    private void RestoreSavedSettings()
    {
        var settings = _store.GetSettings();

        // Audio — driver et buffer (les bus mappings sont restaurés après
        // InitializeFromSavedSettingsAsync, une fois le driver ouvert et
        // AvailableOutputPairs peuplé)
        if (settings.AudioConfig is { } audio)
        {
            if (AvailableDrivers.Contains(audio.DriverName))
                SelectedDriver = audio.DriverName;

            if (AvailableBufferSizes.Contains(audio.BufferSize))
                SelectedBufferSize = audio.BufferSize;
        }

        // MIDI
        if (settings.MidiConfig is { } midi)
        {
            foreach (var port in AvailableMidiPorts)
                port.IsSelected = midi.SelectedPorts.Contains(port.PortName);
        }
    }

    /// <summary>
    /// Initialise les moteurs audio et MIDI avec la configuration sauvegardée.
    /// Appelé au démarrage de l'application.
    /// </summary>
    public async Task InitializeFromSavedSettingsAsync()
    {
        var settings = _store.GetSettings();

        if (settings.AudioConfig is { } audio && !string.IsNullOrEmpty(audio.DriverName))
        {
            await _audioEngine.InitializeAsync(audio);
            AudioInitialized = true;
            AudioStatusMessage = $"Audio restauré — {audio.DriverName}, buffer {audio.BufferSize}";
            AvailableOutputPairs = _audioEngine.GetAvailableOutputPairs();

            // Restaurer les bus mappings maintenant que le driver est ouvert
            // et que AvailableOutputPairs est peuplé
            if (audio.BusMappings.Count > 0)
            {
                BusMappings.Clear();
                foreach (var (busName, outputName) in audio.BusMappings)
                    BusMappings.Add(new BusMapping { BusName = busName, OutputName = outputName });
            }
        }

        if (settings.MidiConfig is { SelectedPorts.Count: > 0 } midi)
        {
            await _midiEngine.InitializeAsync(midi);
            MidiStatusMessage = $"MIDI restauré — {midi.SelectedPorts.Count} port(s)";
        }
    }

    // ------------------------------------------------------------------ //
    // Audio — Commandes
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task ApplyAudioConfigAsync()
    {
        if (SelectedDriver is null) return;

        var config = new AudioConfig
        {
            DriverName = SelectedDriver,
            BufferSize = SelectedBufferSize,
        };

        foreach (var mapping in BusMappings)
            config.BusMappings[mapping.BusName] = mapping.OutputName;

        await _audioEngine.InitializeAsync(config);
        AudioInitialized = true;
        AudioStatusMessage = $"Audio initialisé — {SelectedDriver}, buffer {SelectedBufferSize}";

        // Rafraîchir les sorties disponibles maintenant que le driver est ouvert
        AvailableOutputPairs = _audioEngine.GetAvailableOutputPairs();

        // Persister la configuration audio
        var settings = _store.GetSettings();
        settings.AudioConfig = config;
        _store.SaveSettings(settings);
    }

    // ------------------------------------------------------------------ //
    // MIDI — Commandes
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task ApplyMidiConfigAsync()
    {
        var selectedPorts = AvailableMidiPorts
            .Where(p => p.IsSelected)
            .Select(p => p.PortName)
            .ToList();

        if (selectedPorts.Count == 0)
        {
            MidiStatusMessage = "Sélectionnez au moins un port MIDI.";
            return;
        }

        if (selectedPorts.Count > 6)
        {
            MidiStatusMessage = "Maximum 6 ports MIDI autorisés.";
            return;
        }

        var config = new MidiConfig { SelectedPorts = selectedPorts };
        await _midiEngine.InitializeAsync(config);
        MidiStatusMessage = $"MIDI initialisé — {selectedPorts.Count} port(s)";

        // Persister la configuration MIDI
        var settings = _store.GetSettings();
        settings.MidiConfig = config;
        _store.SaveSettings(settings);
    }

    [RelayCommand]
    private async Task TestMidiSendAsync()
    {
        var selectedPorts = AvailableMidiPorts
            .Where(p => p.IsSelected)
            .Select(p => p.PortName)
            .ToList();

        if (selectedPorts.Count == 0)
        {
            MidiTestResult = "Aucun port sélectionné.";
            return;
        }

        // Auto-initialise le MIDI si nécessaire avant le test
        var config = new MidiConfig { SelectedPorts = selectedPorts };
        await _midiEngine.InitializeAsync(config);

        // Envoie un NoteOn de test sur chaque port sélectionné
        foreach (var port in selectedPorts)
        {
            await _midiEngine.SendDirectAsync(MidiEventType.NoteOn, port, 1, 60, 100);
        }

        MidiTestResult = $"Test envoyé sur {selectedPorts.Count} port(s) — NoteOn C4";
    }

    [RelayCommand]
    private void ToggleMidiPort(MidiPortItem port)
    {
        if (port.IsSelected)
        {
            port.IsSelected = false;
        }
        else if (SelectedMidiPortCount < 6)
        {
            port.IsSelected = true;
        }
        else
        {
            MidiStatusMessage = "Maximum 6 ports MIDI.";
        }
    }

    // ------------------------------------------------------------------ //
    // Profils MIDI — Commandes
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void AddMidiProfile()
    {
        var profile = new MidiProfile { Name = $"Profil {MidiProfiles.Count + 1}" };
        MidiProfiles.Add(profile);
        SelectedMidiProfile = profile;
        PersistProfiles();
        ProfileStatusMessage = $"Profil \"{profile.Name}\" créé.";
    }

    [RelayCommand]
    private void DeleteMidiProfile()
    {
        if (SelectedMidiProfile is null) return;

        var name = SelectedMidiProfile.Name;
        MidiProfiles.Remove(SelectedMidiProfile);
        SelectedMidiProfile = MidiProfiles.FirstOrDefault();
        PersistProfiles();
        ProfileStatusMessage = $"Profil \"{name}\" supprimé.";
    }

    [RelayCommand]
    private void RenameMidiProfile(string newName)
    {
        if (SelectedMidiProfile is null || string.IsNullOrWhiteSpace(newName)) return;

        SelectedMidiProfile.Name = newName.Trim();
        // Force UI refresh — replace in collection
        var index = MidiProfiles.IndexOf(SelectedMidiProfile);
        if (index >= 0)
        {
            var profile = SelectedMidiProfile;
            MidiProfiles[index] = profile;
            SelectedMidiProfile = profile;
        }
        PersistProfiles();
    }

    [RelayCommand]
    private void AddPresetToProfile()
    {
        if (SelectedMidiProfile is null) return;

        var preset = new MidiPreset
        {
            Name = string.IsNullOrWhiteSpace(NewPresetName) ? $"Raccourci {CurrentProfilePresets.Count + 1}" : NewPresetName.Trim(),
            Type = NewPresetType,
            Data1 = NewPresetData1,
            Data2 = NewPresetData2,
        };

        SelectedMidiProfile.Presets.Add(preset);
        CurrentProfilePresets.Add(preset);
        SelectedPreset = preset;

        // Reset form
        NewPresetName = string.Empty;
        NewPresetData1 = 0;
        NewPresetData2 = 0;

        PersistProfiles();
        ProfileStatusMessage = $"Raccourci \"{preset.Name}\" ajouté.";
    }

    [RelayCommand]
    private void UpdateSelectedPreset()
    {
        if (SelectedMidiProfile is null || SelectedPreset is null) return;

        SelectedPreset.Name = string.IsNullOrWhiteSpace(NewPresetName)
            ? SelectedPreset.Name
            : NewPresetName.Trim();
        SelectedPreset.Type = NewPresetType;
        SelectedPreset.Data1 = NewPresetData1;
        SelectedPreset.Data2 = NewPresetData2;

        // Refresh dans la liste observable pour mettre à jour l'affichage
        var index = CurrentProfilePresets.IndexOf(SelectedPreset);
        if (index >= 0)
        {
            var preset = SelectedPreset;
            CurrentProfilePresets[index] = preset;
            SelectedPreset = preset;
        }

        PersistProfiles();
        ProfileStatusMessage = $"Raccourci \"{SelectedPreset.Name}\" modifié.";
    }

    [RelayCommand]
    private void DeletePresetFromProfile()
    {
        if (SelectedMidiProfile is null || SelectedPreset is null) return;

        var name = SelectedPreset.Name;
        SelectedMidiProfile.Presets.Remove(SelectedPreset);
        CurrentProfilePresets.Remove(SelectedPreset);
        SelectedPreset = CurrentProfilePresets.LastOrDefault();
        PersistProfiles();
        ProfileStatusMessage = $"Raccourci \"{name}\" supprimé.";
    }

    private void PersistProfiles()
    {
        var settings = _store.GetSettings();
        settings.MidiProfiles = MidiProfiles.ToList();
        _store.SaveSettings(settings);
    }
}

// ------------------------------------------------------------------ //
// Modèles auxiliaires pour la vue
// ------------------------------------------------------------------ //

/// <summary>
/// Représente un mapping bus logique → sortie physique dans la configuration audio.
/// </summary>
public partial class BusMapping : ObservableObject
{
    [ObservableProperty]
    private string _busName = string.Empty;

    [ObservableProperty]
    private string _outputName = string.Empty;
}

/// <summary>
/// Représente un port MIDI disponible avec son état de sélection.
/// </summary>
public partial class MidiPortItem : ObservableObject
{
    [ObservableProperty]
    private string _portName = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
