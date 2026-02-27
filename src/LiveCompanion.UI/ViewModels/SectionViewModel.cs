using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveCompanion.Core.Models;

namespace LiveCompanion.UI.ViewModels;

/// <summary>
/// Sub-ViewModel enveloppant un <see cref="Section"/> avec des propriétés
/// observables et de la validation via Data Annotations.
/// </summary>
public partial class SectionViewModel : ObservableValidator
{
    private readonly Section _model;

    public SectionViewModel(Section model)
    {
        _model = model;
        _name = model.Name;
        _tempo = model.Tempo;
        _barCount = model.BarCount;
        _timeSignatureNumerator = model.TimeSignature.Numerator;
        _timeSignatureDenominator = model.TimeSignature.Denominator;
    }

    public Guid Id => _model.Id;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Le nom est requis.")]
    [MinLength(1)]
    private string _name;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(20.0, 300.0, ErrorMessage = "Le tempo doit être entre 20 et 300 BPM.")]
    private double _tempo;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, int.MaxValue, ErrorMessage = "Le nombre de mesures doit être >= 1.")]
    private int _barCount;

    [ObservableProperty]
    private int _timeSignatureNumerator;

    [ObservableProperty]
    private int _timeSignatureDenominator;

    /// <summary>
    /// Applique les valeurs courantes du ViewModel vers le modèle <see cref="Section"/>.
    /// </summary>
    public void ApplyToModel()
    {
        _model.Name = Name;
        _model.Tempo = Tempo;
        _model.BarCount = BarCount;
        _model.TimeSignature = new TimeSignature(TimeSignatureNumerator, TimeSignatureDenominator);
    }

    /// <summary>Référence vers le modèle sous-jacent.</summary>
    public Section Model => _model;

    /// <summary>Valide toutes les propriétés annotées et met à jour les erreurs.</summary>
    public void Validate() => ValidateAllProperties();

    /// <summary>Indique si le ViewModel contient des erreurs de validation.</summary>
    public new bool HasErrors => ((INotifyDataErrorInfo)this).HasErrors;
}
