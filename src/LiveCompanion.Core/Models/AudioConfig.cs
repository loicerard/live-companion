namespace LiveCompanion.Core.Models;

/// <summary>
/// Configuration du moteur audio (driver, buffer, mapping des bus).
/// </summary>
public class AudioConfig
{
    /// <summary>Nom du driver ASIO sélectionné.</summary>
    public string DriverName { get; set; } = string.Empty;

    /// <summary>Taille du buffer audio en samples.</summary>
    public int BufferSize { get; set; } = 256;

    /// <summary>
    /// Mapping bus logique → sortie physique (ex : "Main" → "Output 1-2").
    /// </summary>
    public Dictionary<string, string> BusMappings { get; init; } = [];
}
