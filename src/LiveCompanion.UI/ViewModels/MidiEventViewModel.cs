using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveCompanion.Core.Models;

namespace LiveCompanion.UI.ViewModels;

/// <summary>
/// Sub-ViewModel enveloppant un <see cref="MidiEvent"/> avec des propriétés
/// observables et de la validation via Data Annotations.
/// </summary>
public partial class MidiEventViewModel : ObservableValidator
{
    private readonly MidiEvent _model;

    public MidiEventViewModel(MidiEvent model)
    {
        _model = model;
        _type = model.Type;
        _deviceOut = model.DeviceOut;
        _channel = model.Channel;
        _data1 = model.Data1;
        _data2 = model.Data2;
        _sectionIndex = model.Position.SectionIndex;
        _bar = model.Position.Bar;
        _beat = model.Position.Beat;
        _tick = model.Position.Tick;
    }

    public Guid Id => _model.Id;

    [ObservableProperty]
    private MidiEventType _type;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Le device de sortie est requis.")]
    private string _deviceOut;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 16, ErrorMessage = "Le canal MIDI doit être entre 1 et 16.")]
    private int _channel;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 127, ErrorMessage = "Data1 doit être entre 0 et 127.")]
    private int _data1;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 127, ErrorMessage = "Data2 doit être entre 0 et 127.")]
    private int _data2;

    // Position dans la timeline
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "L'index de section doit être >= 0.")]
    private int _sectionIndex;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, int.MaxValue, ErrorMessage = "La mesure doit être >= 1.")]
    private int _bar;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, int.MaxValue, ErrorMessage = "Le temps doit être >= 1.")]
    private int _beat;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, int.MaxValue, ErrorMessage = "Le tick doit être >= 0.")]
    private int _tick;

    /// <summary>Indique si Data2 est pertinent pour le type d'événement courant.</summary>
    public bool ShowData2 => Type != MidiEventType.ProgramChange;

    partial void OnTypeChanged(MidiEventType value)
    {
        OnPropertyChanged(nameof(ShowData2));
        OnPropertyChanged(nameof(DisplaySummary));
    }

    /// <summary>Applique les valeurs courantes vers le modèle <see cref="MidiEvent"/>.</summary>
    public void ApplyToModel()
    {
        _model.Type = Type;
        _model.DeviceOut = DeviceOut;
        _model.Channel = Channel;
        _model.Data1 = Data1;
        _model.Data2 = Data2;
        _model.Position = new TimelinePosition(SectionIndex, Bar, Beat, Tick);
    }

    /// <summary>Référence vers le modèle sous-jacent.</summary>
    public MidiEvent Model => _model;

    /// <summary>Valide toutes les propriétés annotées.</summary>
    public void Validate() => ValidateAllProperties();

    /// <summary>Indique si le ViewModel contient des erreurs de validation.</summary>
    public new bool HasErrors => ((INotifyDataErrorInfo)this).HasErrors;

    /// <summary>Résumé affiché dans la liste.</summary>
    public string DisplaySummary => $"{Type} ch.{Channel} → {DeviceOut}";
}
