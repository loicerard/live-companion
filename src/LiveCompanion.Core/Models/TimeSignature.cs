namespace LiveCompanion.Core.Models;

/// <summary>
/// Représente une signature rythmique (ex : 4/4, 3/4, 6/8).
/// </summary>
public record TimeSignature(int Numerator, int Denominator)
{
    /// <summary>Signature par défaut : 4/4.</summary>
    public static readonly TimeSignature Default = new(4, 4);

    public override string ToString() => $"{Numerator}/{Denominator}";
}
