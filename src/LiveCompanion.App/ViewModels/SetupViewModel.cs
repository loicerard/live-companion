using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;
using LiveCompanion.Audio;
using LiveCompanion.Core.Models;
using LiveCompanion.Midi;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// ViewModel for the MIDI / Audio hardware setup view.
/// </summary>
public sealed partial class SetupViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly INavigationService _nav;
    private readonly NotificationViewModel _notification;

    public SetupViewModel(AppServices services, INavigationService nav,
                          NotificationViewModel notification)
    {
        _services     = services;
        _nav          = nav;
        _notification = notification;

        LoadFromConfig();
        RefreshPortLists();
    }

    // ── Audio section ──────────────────────────────────────────────

    public ObservableCollection<string> AsioDrivers { get; } = [];

    [ObservableProperty]
    private string? _selectedAsioDriver;

    [ObservableProperty]
    private int _bufferSize = AudioConfiguration.DefaultBufferSize;

    [ObservableProperty]
    private int _metronomeChannel = AudioConfiguration.DefaultMetronomeChannel;

    [ObservableProperty]
    private int _sampleChannel = AudioConfiguration.DefaultSampleChannel;

    [ObservableProperty]
    private float _masterVolume = AudioConfiguration.DefaultMasterVolume;

    [ObservableProperty]
    private float _strongBeatVolume = AudioConfiguration.DefaultStrongBeatVolume;

    [ObservableProperty]
    private float _weakBeatVolume = AudioConfiguration.DefaultWeakBeatVolume;

    // ── MIDI section ───────────────────────────────────────────────

    public ObservableCollection<string> MidiOutputPorts { get; } = [];
    public ObservableCollection<string> MidiInputPorts  { get; } = [];

    // Quad1
    [ObservableProperty] private string? _quad1PortName;
    [ObservableProperty] private int     _quad1Channel;

    // Quad2
    [ObservableProperty] private string? _quad2PortName;
    [ObservableProperty] private int     _quad2Channel;

    // SSPD
    [ObservableProperty] private string? _sspdPortName;
    [ObservableProperty] private int     _sspdChannel;

    // MIDI IN
    [ObservableProperty] private string? _midiInputPort;

    // MIDI Clock targets
    [ObservableProperty] private bool _clockToQuad1 = true;
    [ObservableProperty] private bool _clockToQuad2 = true;
    [ObservableProperty] private bool _clockToSspd;

    // MIDI Input mappings
    public ObservableCollection<MidiMappingItemViewModel> InputMappings { get; } = [];

    // ── Commands ───────────────────────────────────────────────────

    [RelayCommand]
    private void RefreshPorts()
    {
        RefreshPortLists();
    }

    [RelayCommand]
    private void TestAudio()
    {
        try
        {
            _services.MetronomeAudio?.Start();
            // 4 beats test — auto-stop after 4 beats
            _ = Task.Delay(4 * 60_000 / Math.Max(1, (int)(_services.AudioConfig.MetronomeMasterVolume * 120 + 1)))
                    .ContinueWith(_ => _services.MetronomeAudio?.Stop(), TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            _notification.ShowError($"Audio test failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        try
        {
            var audioConfig = BuildAudioConfig();
            var midiConfig  = BuildMidiConfig();

            ConfigPaths.EnsureBaseDirectoryExists();
            await AudioConfiguration.SaveAsync(audioConfig, ConfigPaths.AudioConfigFile);
            await SaveMidiConfigAsync(midiConfig, ConfigPaths.MidiConfigFile);

            _services.UpdateAudioConfig(audioConfig);
            _services.UpdateMidiConfig(midiConfig);

            _notification.ShowWarning("Configuration saved.");
        }
        catch (Exception ex)
        {
            _notification.ShowError($"Failed to save config: {ex.Message}");
        }
    }

    [RelayCommand]
    private void AddMidiMapping()
    {
        InputMappings.Add(new MidiMappingItemViewModel());
    }

    [RelayCommand]
    private void RemoveMidiMapping(MidiMappingItemViewModel? item)
    {
        if (item is not null) InputMappings.Remove(item);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private void LoadFromConfig()
    {
        var a = _services.AudioConfig;
        SelectedAsioDriver  = a.AsioDriverName;
        BufferSize          = a.BufferSize;
        MetronomeChannel    = a.MetronomeChannelOffset;
        SampleChannel       = a.SampleChannelOffset;
        MasterVolume        = a.MetronomeMasterVolume;
        StrongBeatVolume    = a.StrongBeatVolume;
        WeakBeatVolume      = a.WeakBeatVolume;

        var m = _services.MidiConfig;
        if (m.OutputDevices.TryGetValue(DeviceTarget.Quad1, out var q1))
        { Quad1PortName = q1.PortName; Quad1Channel = q1.Channel; }
        if (m.OutputDevices.TryGetValue(DeviceTarget.Quad2, out var q2))
        { Quad2PortName = q2.PortName; Quad2Channel = q2.Channel; }
        if (m.OutputDevices.TryGetValue(DeviceTarget.SSPD, out var sp))
        { SspdPortName = sp.PortName; SspdChannel = sp.Channel; }

        MidiInputPort = m.MidiInputPortName;
        ClockToQuad1  = m.ClockTargets.Contains(DeviceTarget.Quad1);
        ClockToQuad2  = m.ClockTargets.Contains(DeviceTarget.Quad2);
        ClockToSspd   = m.ClockTargets.Contains(DeviceTarget.SSPD);

        foreach (var mapping in m.InputMappings)
            InputMappings.Add(new MidiMappingItemViewModel(mapping));
    }

    private void RefreshPortLists()
    {
        AsioDrivers.Clear();
        foreach (var d in _services.AsioService.GetAvailableDrivers())
            AsioDrivers.Add(d);

        MidiOutputPorts.Clear();
        MidiInputPorts.Clear();
        foreach (var p in _services.MidiService.GetOutputPortNames())
            MidiOutputPorts.Add(p);
        foreach (var p in _services.MidiService.GetInputPortNames())
            MidiInputPorts.Add(p);
    }

    private AudioConfiguration BuildAudioConfig() => new()
    {
        AsioDriverName        = SelectedAsioDriver,
        BufferSize            = BufferSize,
        MetronomeChannelOffset = MetronomeChannel,
        SampleChannelOffset   = SampleChannel,
        MetronomeMasterVolume = MasterVolume,
        StrongBeatVolume      = StrongBeatVolume,
        WeakBeatVolume        = WeakBeatVolume,
        AutoReconnect         = true,
    };

    private MidiConfiguration BuildMidiConfig() => new()
    {
        OutputDevices = new Dictionary<DeviceTarget, DeviceOutputConfig>
        {
            [DeviceTarget.Quad1] = new() { PortName = Quad1PortName ?? string.Empty, Channel = Quad1Channel },
            [DeviceTarget.Quad2] = new() { PortName = Quad2PortName ?? string.Empty, Channel = Quad2Channel },
            [DeviceTarget.SSPD]  = new() { PortName = SspdPortName  ?? string.Empty, Channel = SspdChannel  },
        },
        MidiInputPortName = MidiInputPort,
        ClockTargets = BuildClockTargets(),
        InputMappings = InputMappings.Select(m => m.ToModel()).ToList(),
    };

    private List<DeviceTarget> BuildClockTargets()
    {
        var targets = new List<DeviceTarget>();
        if (ClockToQuad1) targets.Add(DeviceTarget.Quad1);
        if (ClockToQuad2) targets.Add(DeviceTarget.Quad2);
        if (ClockToSspd)  targets.Add(DeviceTarget.SSPD);
        return targets;
    }

    private static async Task SaveMidiConfigAsync(MidiConfiguration config, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        });
        await File.WriteAllTextAsync(filePath, json);
    }
}

/// <summary>View model for a single MIDI input mapping row.</summary>
public sealed partial class MidiMappingItemViewModel : ObservableObject
{
    public MidiMappingItemViewModel() { }

    public MidiMappingItemViewModel(MidiInputMapping model)
    {
        _statusType = model.StatusType;
        _channel    = model.Channel;
        _data1      = model.Data1;
        _data2      = model.Data2;
        _action     = model.Action;
    }

    [ObservableProperty] private byte       _statusType;
    [ObservableProperty] private int        _channel   = -1;
    [ObservableProperty] private int        _data1     = -1;
    [ObservableProperty] private int        _data2     = -1;
    [ObservableProperty] private MidiAction _action;

    public MidiInputMapping ToModel() => new()
    {
        StatusType = StatusType,
        Channel    = Channel,
        Data1      = Data1,
        Data2      = Data2,
        Action     = Action,
    };
}
