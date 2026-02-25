namespace LiveCompanion.Core.Models;

/// <summary>
/// Position absolue dans la timeline d'un morceau (section / mesure / temps / tick).
/// </summary>
/// <param name="SectionIndex">Index de la section (0-based).</param>
/// <param name="Bar">Mesure dans la section (1-based).</param>
/// <param name="Beat">Temps dans la mesure (1-based).</param>
/// <param name="Tick">Tick dans le temps (0-based, résolution interne).</param>
public record TimelinePosition(int SectionIndex, int Bar, int Beat, int Tick)
{
    /// <summary>Position initiale : début du morceau.</summary>
    public static readonly TimelinePosition Zero = new(0, 1, 1, 0);

    public override string ToString() => $"S{SectionIndex + 1} | {Bar}:{Beat}:{Tick:D3}";
}
