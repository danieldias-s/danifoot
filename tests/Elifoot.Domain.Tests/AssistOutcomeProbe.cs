using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public static class AssistOutcomeProbe
{
    // Freeze the participants and disable coaching decisions: this probe protects match physics,
    // while lineup selection and AI parity are tested separately in LineupSelectionV3Tests.
    public static string FixedLineupHash(int seed)
    {
        var w = WorldSeedFactory.CreateSeededWorld(seed: seed, totalClubs: 64);
        var m = w.Fixtures.First();
        var home = w.Clubs.Single(c => c.Id == m.HomeClubId); var away = w.Clubs.Single(c => c.Id == m.AwayClubId);
        List<Guid> Fixed(Club club) => new[] { (Elifoot.Contracts.PlayerPosition.G, 1), (Elifoot.Contracts.PlayerPosition.D, 4),
            (Elifoot.Contracts.PlayerPosition.M, 4), (Elifoot.Contracts.PlayerPosition.A, 2) }
            .SelectMany(x => w.Players.Where(p => p.ClubId == club.Id && p.Position == x.Item1).OrderBy(p => p.Id).Take(x.Item2)).Select(p => p.Id).ToList();
        home.HumanCoachId = home.Id; away.HumanCoachId = away.Id;
        m.HomeFormation = m.AwayFormation = "4-4-2";
        m.HomeActiveStarterIds = Fixed(home); m.AwayActiveStarterIds = Fixed(away);
        m.HomeBenchIds = []; m.AwayBenchIds = [];
        MatchSimulationEngine.SimulateFullMatchReal(m, home, away, null, null,
            w.Players.Where(p => p.ClubId == home.Id).ToList(), w.Players.Where(p => p.ClubId == away.Id).ToList(),
            w.Players.ToDictionary(p => p.Id), seed);
        var result = new { m.HomeGoals, m.AwayGoals, m.HomeFirstHalfGoals, m.AwayFirstHalfGoals,
            Events = m.Events.Select(e => new { e.Minute, e.EventType, e.ClubId, e.PrimaryPlayerId, e.SecondaryPlayerId, e.Description }),
            Players = w.Players.Select(p => new { p.Id, p.Strength, p.SeasonGoals, p.CareerGoals, p.InjuryDaysRemaining, p.InjuryRoundsRemaining }) };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result))));
    }

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
