using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Starting distributions of the classic seed, used when inventing clubs from scratch.</summary>
internal static class InitialFictionalStrength
{
    public static (int Minimum, int Maximum) Range(DivisionTier tier) => tier switch
    {
        DivisionTier.PrimeiraDivisao => (34, 48),
        DivisionTier.SegundaDivisao => (27, 39),
        DivisionTier.TerceiraDivisao => (21, 32),
        DivisionTier.QuartaDivisao => (16, 26),
        _ => (10, 21)
    };

    public static int FromDraw(DivisionTier tier, ulong draw)
    {
        var (minimum, maximum) = Range(tier);
        // Map the full hash to the inclusive seed range, preserving the order of the draws.
        return minimum + (int)(((UInt128)draw * (uint)(maximum - minimum + 1)) >> 64);
    }
}
