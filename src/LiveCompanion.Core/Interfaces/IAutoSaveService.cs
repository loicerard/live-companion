namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Service de sauvegarde automatique périodique.
/// Sauvegarde les morceaux modifiés sur disque à intervalle régulier.
/// </summary>
public interface IAutoSaveService : IDisposable
{
    /// <summary>Intervalle entre deux sauvegardes (par défaut 5 minutes).</summary>
    TimeSpan Interval { get; set; }

    /// <summary>Indique si le service est actif.</summary>
    bool IsRunning { get; }

    /// <summary>Démarre la sauvegarde automatique.</summary>
    void Start();

    /// <summary>Arrête la sauvegarde automatique.</summary>
    void Stop();

    /// <summary>Force une sauvegarde immédiate de tous les morceaux modifiés.</summary>
    Task SaveNowAsync();
}
