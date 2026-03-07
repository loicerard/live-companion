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

        // Restaurer la configuration sauvegardée
        RestoreSavedSettings();
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
            var testEvent = new MidiEvent
            {
                Type = MidiEventType.NoteOn,
                DeviceOut = port,
                Channel = 1,
                Data1 = 60, // C4
                Data2 = 100,
                Position = new TimelinePosition(0, 1, 1, 0)
            };
            await _midiEngine.SendEventAsync(testEvent);
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
