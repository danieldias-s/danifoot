using Elifoot.Contracts;

namespace Elifoot.Domain;

internal static class ChampionshipStrengthLimits
{
    private static readonly int[][] Limits =
    [
        [50, 50, 50, 46, 44, 42, 40, 39],
        [38, 37, 36, 34, 32, 30, 28, 27],
        [25, 24, 23, 21, 19, 17, 15, 14],
        [12, 11, 10, 8, 6, 4, 2, 1]
    ];

    public static int For(DivisionTier division, int position) =>
        division is >= DivisionTier.PrimeiraDivisao and <= DivisionTier.QuartaDivisao
            ? Limits[(int)division - 1][Math.Clamp(position, 1, 8) - 1] : 1;
}
