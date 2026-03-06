using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.Core.Models;

namespace LiveCompanion.UI.ViewModels;

/// <summary>
/// Sub-ViewModel enveloppant un <see cref="AudioClip"/> avec des propriétés
/// observables et de la validation via Data Annotations.
/// </summary>
public partial class AudioClipViewModel : ObservableValidator
{
    private readonly AudioClip _model;

    public AudioClipViewModel(AudioClip model)
    {
        _model = model;
        _name = model.Name;
        _filePath = model.FilePath;
        _fadeInSeconds = model.FadeInSeconds;
        _fadeOutSeconds = model.FadeOutSeconds;
        _syncMode = model.SyncMode;
        _sectionIndex = model.Position.SectionIndex;
        _bar = model.Position.Bar;
        _beat = model.Position.Beat;
        _tick = model.Position.Tick;

        // Initialiser les sends
        Sends = new ObservableCollection<BusSendViewModel>(
            model.Sends.Select(s => new BusSendViewModel(s)));
    }

    public Guid Id => _model.Id;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Le nom est requis.")]
    [MinLength(1)]
    private string _name;

    [ObservableProperty]
    private string _filePath;

    // ------------------------------------------------------------------ //
    // Sends (remplace BusName/Volume)
    // ------------------------------------------------------------------ //

    public ObservableCollection<BusSendViewModel> Sends { get; }

    [ObservableProperty]
    private BusSendViewModel? _selectedSend;

    [RelayCommand]
    private void AddSend()
    {
        var send = new BusSend { BusName = "Main", Volume = 1.0 };
        _model.Sends.Add(send);
        var vm = new BusSendViewModel(send);
        Sends.Add(vm);
        SelectedSend = vm;
        OnPropertyChanged(nameof(DisplaySummary));
    }

    [RelayCommand]
    private void RemoveSend(BusSendViewModel? send)
    {
        if (send is null || Sends.Count <= 1) return; // Garder au moins 1 send
        _model.Sends.Remove(send.Model);
        Sends.Remove(send);
        SelectedSend = Sends.LastOrDefault();
        OnPropertyChanged(nameof(DisplaySummary));
    }

    // ------------------------------------------------------------------ //
    // Autres propriétés
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, double.MaxValue, ErrorMessage = "Le fade-in doit être >= 0.")]
    private double _fadeInSeconds;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, double.MaxValue, ErrorMessage = "Le fade-out doit être >= 0.")]
    private double _fadeOutSeconds;

    [ObservableProperty]
    private SyncMode _syncMode;

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

    /// <summary>Applique les valeurs courantes vers le modèle <see cref="AudioClip"/>.</summary>
    public void ApplyToModel()
    {
        _model.Name = Name;
        _model.FilePath = FilePath;
        _model.FadeInSeconds = FadeInSeconds;
        _model.FadeOutSeconds = FadeOutSeconds;
        _model.SyncMode = SyncMode;
        _model.Position = new TimelinePosition(SectionIndex, Bar, Beat, Tick);

        foreach (var send in Sends)
            send.ApplyToModel();
    }

    /// <summary>Référence vers le modèle sous-jacent.</summary>
    public AudioClip Model => _model;

    /// <summary>Valide toutes les propriétés annotées.</summary>
    public void Validate() => ValidateAllProperties();

    /// <summary>Indique si le ViewModel contient des erreurs de validation.</summary>
    public new bool HasErrors => ((INotifyDataErrorInfo)this).HasErrors;

    /// <summary>Résumé affiché dans la liste.</summary>
    public string DisplaySummary
    {
        get
        {
            var buses = string.Join(", ", Sends.Select(s => s.BusName));
            return $"{Name} — {buses} — {SyncMode}";
        }
    }
}
