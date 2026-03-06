namespace LiveCompanion.Core.Models;

/// <summary>
/// Représente un envoi audio vers un bus de sortie avec un volume indépendant.
/// Utilisé dans <see cref="AudioClip.Sends"/> pour le routage multi-bus.
/// </summary>
public class BusSend
{
    /// <summary>Nom du bus de sortie cible (ex: "Main", "Click", "FX").</summary>
    public string BusName { get; set; } = "Main";

    /// <summary>Volume de l'envoi (0.0 = silence, 1.0 = plein volume).</summary>
    public double Volume { get; set; } = 1.0;
}
