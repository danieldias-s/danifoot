using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;

namespace Elifoot.Domain;

internal static class InitialSquadStrength017B
{
    private static readonly (int Minimum, int Maximum)[] DivisionRanges = [(39, 50), (26, 38), (13, 25), (1, 12)];

    public static InitialSquadReference017B Reference(int division, int position)
    {
        int strength = ChampionshipStrengthLimits.For((DivisionTier)division, position);
        var (minimum, maximum) = DivisionRanges[division - 1];
        return division == 1 && position <= 3
            ? new(position, strength, 47, 50)
            : new(position, strength, Math.Max(minimum, strength - 1), Math.Min(maximum, strength + 1));
    }

    private static ulong Hash(int seed, string clubKey, string playerKey)
    {
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new { Rule = "017B", seed, clubKey, playerKey });
        return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(input));
    }

    public static int[] Draw(int seed, string clubKey, InitialSquadReference017B reference, IEnumerable<string> playerKeys)
    {
        var hashes = playerKeys.Select(key => Hash(seed, clubKey, key)).ToArray();
        uint width = (uint)(reference.Maximum - reference.Minimum + 1);
        var strengths = hashes.Select(hash => reference.Minimum + (int)(hash % width)).ToArray();

        if (reference is { Minimum: 47, Maximum: 50 })
        {
            // Reserve one draw of each value for four seed-selected athletes, then keep the other draws independent.
            int offset = (int)(Hash(seed, clubKey, "") % width);
            var chosen = Enumerable.Range(0, hashes.Length).OrderBy(i => hashes[i]).Take(4);
            foreach (int index in chosen)
            {
                strengths[index] = 47 + offset;
                offset = (offset + 1) % 4;
            }
        }

        return strengths;
    }

    public static List<WorldPreviewClub> Apply(IEnumerable<WorldPreviewClub> clubs, int seed)
    {
        var positions = new int[5];
        return clubs.Select(entry =>
        {
            if (entry.Division is < 1 or > 4 || ++positions[entry.Division] > 8)
                return entry;

            var reference = Reference(entry.Division, positions[entry.Division]);
            var club = entry.Club;
            var strengths = Draw(seed, club.Key, reference,
                club.Players.Select(p => p.CanonicalPlayerId ?? $"{p.Source}:{p.PlayerExternalId}"));
            return entry with
            {
                InitialSquadReference = reference,
                Club = club with { Players = club.Players.Select((p, i) => p with { Strength = strengths[i] }).ToList() }
            };
        }).ToList();
    }
}
