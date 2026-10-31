using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Game estimates; never an official individual provider rating. No gender multiplier.</summary>
public static class InitialPlayerStrength
{
    public const string Version = "relative-v1";
    private static double Unit(string key, int seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{Version}|{seed}|{key}"));
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes) / (double)uint.MaxValue;
    }
    private static double Percentile(double value, IReadOnlyCollection<double> sample)
        => sample.Count <= 1 ? .5 : (sample.Count(x => x < value) + .5 * sample.Count(x => x == value)) / sample.Count;
    public static List<EligibleCatalogClub> Apply(IReadOnlyList<EligibleCatalogClub> clubs, int seed)
    {
        var candidates = clubs.SelectMany(c => c.Players).Where(p => p.StrengthEstimate?.Method.StartsWith(Version) == true).ToList();
        var ranks = clubs.Where(c => c.Rank != null).Select(c => (double)c.Rank!.Value).ToList();
        return clubs.Select(club => club with { Players = club.Players.Select(player => {
            if (player.StrengthEstimate?.Method.StartsWith(Version) != true) return player;
            var cohort = candidates.Where(p => p.Gender == player.Gender && p.Season == player.Season && p.Position == player.Position &&
                p.Performance?.RatingScale == player.Performance?.RatingScale).ToList();
            double? clubPercentile = club.Rank == null ? null : 1 - Percentile(club.Rank.Value, ranks);
            double individual = .5, confidence = .2;
            string method = Version + ":missing-individual";
            var ratingSample = cohort.Where(p => p.Performance?.Rating != null).Select(p => p.Performance!.Rating!.Value).ToList();
            var minuteSample = cohort.Where(p => p.Performance?.Minutes != null).Select(p => (double)p.Performance!.Minutes!.Value).ToList();
            if (player.Performance?.Rating is { } rating && ratingSample.Count >= 3)
            {
                individual = Percentile(rating, ratingSample); confidence = .75; method = Version + ":relative-position-rating";
            }
            else if (player.Performance?.Minutes is { } minutes && minuteSample.Count >= 3)
            {
                individual = Percentile(minutes, minuteSample); confidence = .4; method = Version + ":relative-position-minutes";
            }
            string key = player.CanonicalPlayerId ?? $"{player.Source}:{player.PlayerExternalId}";
            double noise = Unit(key, seed) * 2 - 1;
            double raw = confidence > .2 ? 12 + 37 * individual + 2 * ((clubPercentile ?? .5) - .5) + noise
                : 26 + 16 * ((clubPercentile ?? .5) - .5) + 7 * noise;
            int strength = Math.Clamp((int)Math.Round(raw, MidpointRounding.AwayFromZero), 1, 50);
            var input = new { Version, seed, key, player.Position, player.Performance, clubPercentile, individual };
            string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
            return player with { Strength = strength, StrengthEstimate = new(method, CatalogDataOrigin.Estimated, confidence, hash, individual, clubPercentile) };
        }).ToList() }).ToList();
    }
}
