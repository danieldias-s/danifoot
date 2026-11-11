using System.Text.Json;
using System.Text.Json.Nodes;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

/// <summary>
/// V3 — Etapa 02D: Pure Domain tests for CoachCareerEngine, CompetitionAndSeasonEngine,
/// season transition idempotency, club-coach invariants, and career history preservation.
/// </summary>
public class ManagerCareerV3Stage02DTests
{
    [Fact]
    public void SeasonTransition_PromotionsRelegationsTitlesAndIdempotentFinalize()
    {
        var world = WorldSeedFactory.CreateSeededWorld("V3 Stage 02D Domain", 199801);
        var clubPro2 = world.Clubs.First(c => c.DivisionTier == DivisionTier.SegundaDivisao);
        var clubRel2 = world.Clubs.Last(c => c.DivisionTier == DivisionTier.SegundaDivisao);
        var clubRel4 = world.Clubs.Last(c => c.DivisionTier == DivisionTier.QuartaDivisao);

        HumanCoach AssignHuman(Club club, string key)
        {
            var oldAi = world.AiManagers.Single(a => a.Id == club.AiManagerId);
            oldAi.CurrentClubId = null;
            club.AiManagerId = null;
            var h = new HumanCoach
            {
                Id = CoachCareerEngine.StableId(key),
                GameWorldId = world.Id,
                Nickname = key,
                CurrentClubId = club.Id
            };
            h.EmploymentHistory.Add(new CoachEmploymentRecord
            {
                ClubId = club.Id,
                ClubName = club.Name,
                StartSeason = 1,
                StartRound = 1
            });
            club.HumanCoachId = h.Id;
            world.HumanCoaches.Add(h);
            return h;
        }

        var coachPro = AssignHuman(clubPro2, "dom-coach-pro");
        var coachRel2 = AssignHuman(clubRel2, "dom-coach-rel2");
        var coachRel4 = AssignHuman(clubRel4, "dom-coach-rel4");

        var leagueFixtures = world.Fixtures.Where(f => f.SeasonNumber == 1 && f.CompetitionType == CompetitionType.League).ToList();
        foreach (var f in leagueFixtures)
        {
            CoachCareerEngine.CaptureLeadership(world, f);
            f.CurrentMinute = 90;
            f.IsFinished = true;
            if (f.HomeClubId == clubPro2.Id) { f.HomeGoals = 3; f.AwayGoals = 0; }
            else if (f.AwayClubId == clubPro2.Id) { f.HomeGoals = 0; f.AwayGoals = 3; }
            else if (f.HomeClubId == clubRel2.Id || f.HomeClubId == clubRel4.Id) { f.HomeGoals = 0; f.AwayGoals = 2; }
            else if (f.AwayClubId == clubRel2.Id || f.AwayClubId == clubRel4.Id) { f.HomeGoals = 2; f.AwayGoals = 0; }
            else { f.HomeGoals = 1; f.AwayGoals = 1; }
        }
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(world, leagueFixtures);

        var history1 = CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(world, expectedSeasonNumber: 1);

        Assert.Contains(clubPro2.Id, history1.PromotedClubIds);
        Assert.Equal(DivisionTier.PrimeiraDivisao, clubPro2.DivisionTier);
        Assert.Equal(clubPro2.Id, coachPro.CurrentClubId);

        Assert.Contains(clubRel2.Id, history1.RelegatedClubIds);
        Assert.Equal(DivisionTier.TerceiraDivisao, clubRel2.DivisionTier);
        Assert.Equal(clubRel2.Id, coachRel2.CurrentClubId);

        Assert.Contains(clubRel4.Id, history1.RelegatedClubIds);
        Assert.Equal(DivisionTier.Distrital, clubRel4.DivisionTier);
        Assert.Null(coachRel4.CurrentClubId);
        Assert.True(coachRel4.IsUnemployed);
        Assert.NotNull(clubRel4.AiManagerId);

        Assert.Equal(4, world.CoachTitles.Count(t => t.Season == 1 && t.TitleType == "LEAGUE"));
        Assert.Equal(1, coachPro.LeagueTitles);

        // Idempotent repeat of FinalizeSeasonAndAdvance for season 1
        int seasonsManagedBefore = coachPro.SeasonsManaged;
        int titlesBefore = coachPro.LeagueTitles;
        int dismissalsBefore = coachRel4.Dismissals;
        var historyRepeat = CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(world, expectedSeasonNumber: 1);
        Assert.Same(history1, historyRepeat);
        Assert.Equal(2, world.SeasonNumber);
        Assert.Equal(seasonsManagedBefore, coachPro.SeasonsManaged);
        Assert.Equal(titlesBefore, coachPro.LeagueTitles);
        Assert.Equal(dismissalsBefore, coachRel4.Dismissals);
    }

    [Fact]
    public void Hire_ClearsAnyStaleHumanSubmittedLineupEvenWhenPreviouslyUnemployed()
    {
        var world = WorldSeedFactory.CreateSeededWorld("V3 Stage 02D Hire", 199801);
        world.RoundNumber = 3;
        var target = world.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var human = new HumanCoach
        {
            Id = CoachCareerEngine.StableId("unemp-stale-lineup"),
            GameWorldId = world.Id,
            Nickname = "UnemployedStale",
            CurrentClubId = null,
            IsReadyForRound = true,
            SubmittedFormation = "5-3-2",
            SubmittedStarterIds = [Guid.NewGuid()],
            SubmittedBenchIds = [Guid.NewGuid()]
        };
        world.HumanCoaches.Add(human);
        foreach (var ai in world.AiManagers) ai.Career.Morale = 20;

        CoachCareerEngine.OpenVacancy(world, target);
        CoachCareerEngine.ProcessVacancies(world);
        var invite = world.JobInvitations.Single(i => i.CoachId == human.Id && i.ClubId == target.Id);

        var res = CoachCareerEngine.Respond(world, human.Id, invite.Id, accept: true);
        Assert.True(res.Success, res.Error);
        Assert.Equal(target.Id, human.CurrentClubId);
        Assert.False(human.IsReadyForRound);
        Assert.Null(human.SubmittedFormation);
        Assert.Empty(human.SubmittedStarterIds);
        Assert.Empty(human.SubmittedBenchIds);
    }
}
