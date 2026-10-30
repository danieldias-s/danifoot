using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Api;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class Discipline012CIntegrationTests
{
    [Fact]
    public async Task SqliteMigrationAndRestartKeepPlayerCyclesCardHistoryAndRemainingMatchBans()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var services=new ServiceCollection();services.AddDbContext<ElifootDbContext>(o=>o.UseSqlite(connection));
        await using var provider=services.BuildServiceProvider();
        using(var scope=provider.CreateScope())await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        var repository=new EfWorldRepository(provider.GetRequiredService<IServiceScopeFactory>());
        var w=WorldSeedFactory.CreateSeededWorld();var p=w.Players[0];
        w.DisciplineRulesVersion=0;p.DisciplineVersion=0;p.SeasonYellows=7;p.YellowCardsAccumulated=2;p.SuspensionRoundsRemaining=2;
        await repository.SaveWorldAsync(w);EfWorldRepository.ClearCacheForTesting();
        w=(await repository.GetWorldAsync(w.Id))!;p=w.Players.Single(x=>x.Id==p.Id);
        Assert.Equal(7,p.TotalYellowCards);Assert.Equal(2,p.YellowCardsInCurrentCycle);Assert.Equal(2,p.SuspensionMatchesRemaining);
        Assert.Empty(p.CardHistory);Assert.NotEmpty(w.DisciplineCoverageWarnings);
        var m=new MatchFixture {GameWorldId=w.Id,SeasonNumber=1,RoundNumber=3,HomeClubId=p.ClubId!.Value,
            AwayClubId=w.Clubs.First(c=>c.Id!=p.ClubId).Id,CompetitionType=CompetitionType.League,CurrentMinute=1};
        DisciplineEngine.CaptureKickoff(m,w.Players.ToDictionary(p=>p.Id));m.CurrentMinute=90;m.IsFinished=true;
        DisciplineEngine.SettleFinishedMatches(w,[m]);Assert.Equal(1,p.SuspensionMatchesRemaining);
        await repository.SaveWorldAsync(w);EfWorldRepository.ClearCacheForTesting();
        w=(await repository.GetWorldByInviteCodeAsync(w.InviteCode))!;p=w.Players.Single(x=>x.Id==p.Id);
        var before=JsonSerializer.Serialize(w);DisciplineEngine.SettleFinishedMatches(w,[m]);Assert.Equal(before,JsonSerializer.Serialize(w));
        Assert.Equal(1,p.SuspensionMatchesRemaining);Assert.Single(p.SuspensionHistory);
        Assert.Equal(2,p.YellowCardsInCurrentCycle);Assert.Equal(7,p.TotalYellowCards);
    }

    [Theory]
    [InlineData(2)] [InlineData(4)] [InlineData(6)] [InlineData(8)]
    public async Task IndependentHttpClientsSeeTheSameRealInjuryManDownCountAndOnePersistedEpisode(int count)
    {
        await using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(builder=>{
            builder.UseSetting("Elifoot:DatabaseProvider","InMemory");builder.UseSetting("Elifoot:AutoSeedDefaultWorld","false");
            builder.UseSetting("Elifoot:DisableBackgroundClock","true");});
        using var scope=factory.Services.CreateScope();
        var orchestrator=scope.ServiceProvider.GetRequiredService<MultiplayerRoundOrchestrator>();
        var repo=factory.Services.GetRequiredService<IWorldRepository>();
        var w=await orchestrator.CreateWorldAsync(new CreateWorldRequest($"Discipline {count}",MaxHumans:count));
        w.RoundNumber=3;ClassicForceEvolutionEngine.PrepareRound(w);
        foreach(var p in w.Players)p.Behavior=PlayerBehavior.FairPlay;
        var fixtures=MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(w);
        var clubIds=fixtures.Take(count/2).SelectMany(m=>new[]{m.HomeClubId,m.AwayClubId}).ToList();
        var coaches=new List<HumanCoach>();
        foreach(var clubId in clubIds)
        {
            var joined=await orchestrator.JoinWorldAndAssignClubAsync(w.Id,Guid.NewGuid(),"Coach","disc-client",clubId);
            coaches.Add(joined.Coach!);
        }
        foreach(var coach in coaches)
            Assert.True((await orchestrator.SubmitUnemployedReadyAsync(new(w.Id,coach.Id,$"disc-ready-{coach.Id}",w.RoundVersion))).Success);
        var match=fixtures[0];var map=w.Players.ToDictionary(p=>p.Id);
        foreach(var fixture in fixtures){PlayerStatisticsEngine.CaptureLineup(fixture,map);fixture.CurrentMinute=74;fixture.LastSimulatedMinute=74;}
        match.HomeUsedSubstitutions=5;match.HomeUsedPauseWindows=3;
        w.CurrentMatchMinute=74;w.RoundState=RoundState.RUNNING_SECOND_HALF;
        int index=match.HomeActiveStarterIds.FindIndex(id=>map[id].Position==PlayerPosition.G);
        w.Seed=InjurySeed(match,index);
        await repo.SaveWorldAsync(w);
        var live=(await orchestrator.AdvanceClockTickAsync(w.Id))!;
        Assert.Equal(75,live.CurrentMinute);Assert.Equal(10,live.Matches.Single(m=>m.MatchId==match.Id).HomePlayersOnPitch);
        var injury=Assert.Single(match.Events,e=>e.EventType==MatchEventType.Injury&&e.ClubId==match.HomeClubId);
        var injured=map[injury.PrimaryPlayerId!.Value];Assert.Single(injured.ForceInjuries);Assert.InRange(injured.InjuryDaysRemaining,2,15);
        var clients=Enumerable.Range(0,count).Select(_=>factory.CreateClient()).ToList();
        try
        {
            var snapshots=await Task.WhenAll(clients.Select(c=>c.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{w.Id}/snapshot")));
            foreach(var snap in snapshots)
            {
                Assert.Equal(live.RoundVersion,snap!.RoundVersion);Assert.Equal(live.GameDate,snap.GameDate);
                var view=snap.Matches.Single(m=>m.MatchId==match.Id);Assert.Equal(10,view.HomePlayersOnPitch);
                var row=Assert.Single(view.HomeStarters,p=>p.PlayerId==injured.Id);Assert.True(row.IsInjuredInMatch);Assert.False(row.IsOnPitch);
            }
            Assert.Single(injured.ForceInjuries);Assert.Equal(1,match.Events.Count(e=>e.EventType==MatchEventType.Injury&&e.ClubId==match.HomeClubId));
        }
        finally{foreach(var client in clients)client.Dispose();}
    }
    private static int InjurySeed(MatchFixture m,int index)
    {
        for(int seed=1;seed<=200000;seed++)
        {
            var rng=DeterministicRng.FromContext(seed,m.SeasonNumber,m.RoundNumber,m.DeterministicMatchIndex,75);
            if(rng.NextDouble()<.0018&&rng.NextInt(0,11)==index)return seed;
        }
        throw new InvalidOperationException("Injury context not found.");
    }
}
