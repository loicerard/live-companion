namespace LiveCompanion.Core.Models;

public record TimeSignature(int Numerator, int Denominator)
{
    public static TimeSignature Common => new(4, 4);
    public static TimeSignature Waltz => new(3, 4);
}
