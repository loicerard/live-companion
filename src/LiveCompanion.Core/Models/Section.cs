namespace LiveCompanion.Core.Models;

/// <summary>
/// Section d'un morceau (ex : Intro, Couplet, Refrain).
/// Chaque section possède son propre tempo, sa signature et son nombre de mesures.
/// </summary>
public class Section
{
    /// <summary>Identifiant unique de la section.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Nom affiché de la section.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Tempo en BPM. Compris entre 20 et 300.</summary>
    public double Tempo { get; set; } = 120.0;

    /// <summary>Signature rythmique de la section.</summary>
    public TimeSignature TimeSignature { get; set; } = TimeSignature.Default;

    /// <summary>Nombre de mesures. Doit être supérieur ou égal à 1.</summary>
    public int BarCount { get; set; } = 4;

    /// <summary>Position d'affichage dans la liste des sections (0-based).</summary>
    public int Order { get; set; }
}
