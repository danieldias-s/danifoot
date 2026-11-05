using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public static class AssistOutcomeProbe
{
    public static string Hash(int seed)
    {
        var w = WorldSeedFactory.CreateSeededWorld(seed: seed, totalClubs: 64);
        var m = w.Fixtures.First();
        var home = w.Clubs.Single(c => c.Id == m.HomeClubId);
        var away = w.Clubs.Single(c => c.Id == m.AwayClubId);
        MatchSimulationEngine.SimulateFullMatchReal(m, home, away,
            w.AiManagers.Single(a => a.Id == home.AiManagerId), w.AiManagers.Single(a => a.Id == away.AiManagerId),
            w.Players.Where(p => p.ClubId == home.Id).ToList(), w.Players.Where(p => p.ClubId == away.Id).ToList(),
            w.Players.ToDictionary(p => p.Id), seed);
        // Deliberately excludes only the newly added observational assist metadata.
        var result = new {
            m.HomeGoals, m.AwayGoals, m.HomeFirstHalfGoals, m.AwayFirstHalfGoals, m.HomePenaltyGoals, m.AwayPenaltyGoals,
            Events = m.Events.Select(e => new { e.Minute, e.EventType, e.ClubId, e.PrimaryPlayerId, e.SecondaryPlayerId, e.Description }),
            Players = w.Players.Select(p => new { p.Id, p.Strength, p.SeasonGoals, p.CareerGoals, p.InjuryDaysRemaining, p.InjuryRoundsRemaining })
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result))));
    }
}
