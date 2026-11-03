using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Api;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class ClassicForce012CIntegrationTests
{
    [Fact]
    public async Task SqlitePersistsDailyLossDatesRecoveryAndHistoryWithoutReapplying()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<ElifootDbContext>(o=>o.UseSqlite(connection));
        builder.Services.AddSingleton<IWorldRepository,EfWorldRepository>();
        builder.Services.AddSingleton<PlayerRankingsApplicationService>();
        await using var app=builder.Build();app.MapPlayerRankings();
        using(var scope=app.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        await app.StartAsync();
        var repo=app.Services.GetRequiredService<IWorldRepository>();
        var w=WorldSeedFactory.CreateSeededWorld(forceEvolutionMode:ForceEvolutionMode.ClassicAdapted);
        var p=w.Players[0];p.Strength=50;
        var injury=new ForceInjury {Id=Guid.NewGuid(),MatchId=Guid.NewGuid(),Season=1,Round=1,
            StartDate=w.ClassicForce.GameDate,Duration=15,InitialForce=50,TotalLoss=25};
        p.ForceInjuries.Add(injury);p.InjuryDaysRemaining=15;
        ClassicForceEvolutionEngine.AdvanceToDate(w,injury.StartDate.AddDays(8));
        Assert.Equal(37,p.Strength);
        await repo.SaveWorldAsync(w);EfWorldRepository.ClearCacheForTesting();
        var loaded=(await repo.GetWorldAsync(w.Id))!;
        Assert.NotSame(w,loaded);
        var lp=loaded.Players.Single(x=>x.Id==p.Id);
        Assert.Equal(7,lp.InjuryDaysRemaining);
        ClassicForceEvolutionEngine.AdvanceToDate(loaded,injury.EndDate);
        Assert.Equal(25,lp.Strength);Assert.True(lp.IsAvailableForMatch);
        Assert.Equal(25,loaded.PlayerStrengthHistory.Sum(e=>e.DailyInjuryLoss));
        string before=JsonSerializer.Serialize(loaded.PlayerStrengthHistory);
        ClassicForceEvolutionEngine.AdvanceToDate(loaded,injury.EndDate);
        Assert.Equal(before,JsonSerializer.Serialize(loaded.PlayerStrengthHistory));
        await repo.SaveWorldAsync(loaded);EfWorldRepository.ClearCacheForTesting();
        loaded=(await repo.GetWorldByInviteCodeAsync(w.InviteCode))!;
        Assert.Equal(ForceEvolutionMode.ClassicAdapted,loaded.ForceEvolutionMode);
        Assert.Equal(15,loaded.Players.Single(x=>x.Id==p.Id).ForceInjuries.Single().ProcessedDays);
        using var client=app.GetTestClient();
        var history=await client.GetFromJsonAsync<PlayerHistoryDto>($"/api/worlds/{w.Id}/players/{p.Id}/history");
        Assert.NotNull(history);
        Assert.Equal(25,history.Player.Strength);
        Assert.All(history.StrengthHistory,e=>{Assert.Equal(w.Id,e.WorldId);Assert.NotNull(e.GameDate);Assert.Equal(injury.Id,e.InjuryId);});
        Assert.Equal(before,JsonSerializer.Serialize(loaded.PlayerStrengthHistory));
    }

    [Fact]
    public async Task EliminatedCupCoachCanBeReadyWithoutFieldingInjuredPlayersOrAdvancingTheirDays()
    {
        var repo=new InMemoryWorldRepository();
        var orchestrator=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        var w=await orchestrator.CreateWorldAsync(new CreateWorldRequest("Idle012C"));
        w.RoundNumber=3;
        CompetitionAndSeasonEngine.EnsureCupFixturesForRound(w,3);
        var club=w.Clubs.First(c=>c.DivisionTier==DivisionTier.PrimeiraDivisao);
        foreach(var match in w.Fixtures.Where(m=>m.RoundNumber==3))
        {
            match.IsFinished=true;
            match.WinnerClubId=match.AwayClubId==club.Id?match.HomeClubId:match.AwayClubId;
        }
        w.RoundNumber=6;
        CompetitionAndSeasonEngine.EnsureCupFixturesForRound(w,6);
        ClassicForceEvolutionEngine.PrepareRound(w);
        var (_,_,coach,_)=await orchestrator.JoinWorldAndAssignClubAsync(w.Id,Guid.NewGuid(),"Idle coach","idle-012c",club.Id);
        foreach(var p in w.Players.Where(p=>p.ClubId==club.Id))p.InjuryDaysRemaining=15;
        var date=w.ClassicForce.GameDate;
        var response=await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(w.Id,coach!.Id,"4-4-2",[],[],"idle-ready",w.RoundVersion));
        Assert.True(response.Success,response.Error);Assert.True(response.RoundStarted);
        Assert.Equal(date,w.ClassicForce.GameDate);
        Assert.Empty(coach.SubmittedStarterIds);
        Assert.DoesNotContain(MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(w),m=>m.HomeClubId==club.Id||m.AwayClubId==club.Id);
        Assert.Empty(w.PlayerStrengthHistory);
        w.RoundState=RoundState.PREPARING;w.RoundNumber=7;coach.IsReadyForRound=false;
        var rejected=await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(w.Id,coach.Id,"4-4-2",[],[],"playing-needs-eleven",w.RoundVersion));
        Assert.False(rejected.Success);Assert.False(coach.IsReadyForRound);
    }

    [Fact]
    public async Task NewRoomsUseOneAuthoritativeVersionAndConcurrentAdvanceDoesNotRepeatDays()
    {
        var repo=new InMemoryWorldRepository();
        var orchestrator=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        var w=await orchestrator.CreateWorldAsync(new CreateWorldRequest("Classic012C"));
        Assert.Equal(ForceEvolutionMode.ClassicAdapted,w.ForceEvolutionMode);
        var p=w.Players[0];p.Strength=50;
        p.ForceInjuries.Add(new ForceInjury {Id=Guid.NewGuid(),StartDate=w.ClassicForce.GameDate,Duration=15,InitialForce=50,TotalLoss=25});
        p.InjuryDaysRemaining=15;
        w.RoundState=RoundState.RESULTS;
        await Task.WhenAll(Enumerable.Range(0,8).Select(_=>orchestrator.AdvanceToNextRoundPreparationAsync(w.Id)));
        Assert.Equal(2,w.RoundNumber);
        Assert.Equal(new DateOnly(1998,8,5),w.ClassicForce.GameDate);
        Assert.Equal(44,p.Strength);
        Assert.Equal(6,w.PlayerStrengthHistory.Sum(e=>e.DailyInjuryLoss));
        var replies=await Task.WhenAll(Enumerable.Range(0,8).Select(async _=>
        {
            var query=new PlayerRankingsApplicationService(repo);
            return await query.ReadWorldAsync(w.Id,world=>MultiplayerRoundOrchestrator.BuildLiveRoundSnapshotDto(world));
        }));
        Assert.All(replies,s=>{Assert.Equal(ForceEvolutionMode.ClassicAdapted,s!.ForceEvolutionMode);Assert.Equal(1,s.ForceRulesVersion);Assert.Equal(w.ClassicForce.GameDate,s.GameDate);});
        var legacy=await orchestrator.CreateWorldAsync(new CreateWorldRequest("Legacy012C",ForceEvolutionMode:ForceEvolutionMode.LegacySeasonal));
        Assert.Equal(ForceEvolutionMode.LegacySeasonal,legacy.ForceEvolutionMode);
        Assert.Equal(0,legacy.ClassicForce.CalendarSeason);
    }
}
