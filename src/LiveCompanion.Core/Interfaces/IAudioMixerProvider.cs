namespace LiveCompanion.Core.Interfaces;

/// <summary>
/// Fournit un contrôle de volume master par bus en temps réel.
/// </summary>
public interface IAudioMixerProvider
{
    /// <summary>Retourne les noms des bus configurés.</summary>
    IReadOnlyList<string> GetBusNames();

    /// <summary>Retourne le volume master actuel d'un bus (0.0–1.0).</summary>
    float GetBusVolume(string busName);

    /// <summary>Définit le volume master d'un bus (clampé 0.0–1.0).</summary>
    void SetBusVolume(string busName, float volume);
}
