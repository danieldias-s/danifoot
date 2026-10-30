namespace Elifoot.Domain;

/// <summary>
/// Deterministic SplitMix64 pseudo-random number generator.
/// Ensures 100% reproducible match ticks, cup draws, stadium wear, and market simulations
/// from (WorldSeed, Season, Round, Match, Minute, Step) without relying on System.Random implementation details.
/// </summary>
public sealed class DeterministicRng
{
    private ulong _state;

    public DeterministicRng(long seed)
    {
        _state = unchecked((ulong)seed) ^ 0x9E3779B97F4A7C15UL;
        if (_state == 0)
        {
            _state = 0xBF58476D1CE4E5B9UL;
        }
    }

    public static DeterministicRng FromContext(int worldSeed, int season, int round = 0, int matchIndex = 0, int minute = 0, int subStep = 0)
    {
        ulong hash = unchecked((ulong)worldSeed) * 0x9E3779B97F4A7C15UL;
        hash ^= unchecked((ulong)season) * 0xBF58476D1CE4E5B9UL;
        hash ^= unchecked((ulong)round) * 0x94D049BB133111EBUL;
        hash ^= unchecked((ulong)matchIndex) * 0xD6E8FEB86659FD93UL;
        hash ^= unchecked((ulong)minute) * 0xA5A3564E27F88659UL;
        hash ^= unchecked((ulong)subStep) * 0x6C8E9CF570932BD5UL;
        return new DeterministicRng(unchecked((long)hash));
    }

    public ulong NextUInt64()
    {
        ulong z = (_state += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// Returns true with the given probability in [0.0, 1.0].
    /// </summary>
    public bool NextBool(double probability) => NextDouble() < probability;

    /// <summary>
    /// Returns a double in [0.0, 1.0).
    /// </summary>
    public double NextDouble()
    {
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// Returns a double in [minInclusive, maxExclusive).
    /// </summary>
    public double NextDouble(double minInclusive, double maxExclusive)
    {
        return minInclusive + NextDouble() * (maxExclusive - minInclusive);
    }

    /// <summary>
    /// Returns an int in [minInclusive, maxExclusive).
    /// </summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        ulong range = (ulong)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt64() % range);
    }

    public T PickOne<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0) throw new InvalidOperationException("Cannot pick from an empty collection.");
        return items[NextInt(0, items.Count)];
    }

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
