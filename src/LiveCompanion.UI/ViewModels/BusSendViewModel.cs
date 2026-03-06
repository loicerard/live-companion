using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveCompanion.Core.Models;

namespace LiveCompanion.UI.ViewModels;

/// <summary>
/// ViewModel pour un envoi audio vers un bus de sortie.
/// Wraps un <see cref="BusSend"/> avec validation et notification MVVM.
/// </summary>
public partial class BusSendViewModel : ObservableValidator
{
    private readonly BusSend _model;

    public BusSendViewModel(BusSend model)
    {
        _model = model;
        _busName = model.BusName;
        _volume = model.Volume;
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Le bus est requis.")]
    private string _busName;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.0, 1.0, ErrorMessage = "Le volume doit être entre 0 et 1.")]
    private double _volume;

    /// <summary>Applique les valeurs courantes vers le modèle <see cref="BusSend"/>.</summary>
    public void ApplyToModel()
    {
        _model.BusName = BusName;
        _model.Volume = Volume;
    }

    public BusSend Model => _model;
}
