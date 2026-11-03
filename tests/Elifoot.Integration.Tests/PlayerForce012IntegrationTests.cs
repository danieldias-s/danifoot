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
public class PlayerForce012IntegrationTests
{
    [Fact]
    public async Task SqliteReloadAndHttpHistoryRetainStrengthEventsAcrossActualTransfer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<ElifootDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddSingleton<IWorldRepository, EfWorldRepository>();
        builder.Services.AddSingleton<PlayerRankingsApplicationService>();
        await using var app = builder.Build();
        app.MapPlayerRankings();
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        await app.StartAsync();
        var repo = app.Services.GetRequiredService<IWorldRepository>();
        var w = WorldSeedFactory.CreateSeededWorld(seed: 199812);
        w.RoundNumber = 22;
        var seller = w.Clubs.First(c => c.DivisionTier == DivisionTier.Distrital);
        var buyer = w.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var p = w.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
        p.Strength = 1;
        p.SeasonGoals = 6;
        string ranking = JsonSerializer.Serialize(CompetitionAndSeasonEngine.GetClassicCoachRanking(w));
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        var first = Assert.Single(w.PlayerStrengthHistory, e => e.PlayerId == p.Id);
        var transfer = MarketAndEconomyEngine.ExecuteTransfer(w, buyer.Id, seller.Id, p.Id, 100_000m, "Force012", "force012-transfer");
        Assert.True(transfer.Success, transfer.ErrorMessage);
        Assert.Equal(2, p.Strength);
        Assert.Equal(2, w.Transactions.Last(t => t.Category == FinancialTransactionCategory.TransferPurchase).SigningDecision!.Strength);
        w.SeasonNumber = 2;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        seller.Name = "Novo nome posterior";
        await repo.SaveWorldAsync(w);
        EfWorldRepository.ClearCacheForTesting();
        var loaded = (await repo.GetWorldAsync(w.Id))!;
        Assert.NotSame(w, loaded);
        Assert.Equal(3, loaded.Players.Single(x => x.Id == p.Id).Strength);
        string before = JsonSerializer.Serialize(loaded.PlayerStrengthHistory);
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(loaded);
        Assert.Equal(before, JsonSerializer.Serialize(loaded.PlayerStrengthHistory));
        using var client = app.GetTestClient();
        var history = await client.GetFromJsonAsync<PlayerHistoryDto>($"/api/worlds/{w.Id}/players/{p.Id}/history");
        Assert.NotNull(history);
        Assert.Equal(2, history.StrengthHistory.Count);
        Assert.Equal(first, history.StrengthHistory.Last());
        Assert.Equal(buyer.Id, history.StrengthHistory.First().ClubId);
        Assert.Equal(seller.Id, history.StrengthHistory.Last().ClubId);
        Assert.Equal(3, history.Player.Strength);
        Assert.Equal(1, history.StrengthHistoryStartSeason);
        Assert.Equal(ranking, JsonSerializer.Serialize(CompetitionAndSeasonEngine.GetClassicCoachRanking(loaded)));
        using var check = app.Services.CreateScope();
        var record = await check.ServiceProvider.GetRequiredService<ElifootDbContext>().Worlds.SingleAsync(x => x.Id == w.Id);
        string snapshotBefore = record.SnapshotJson;
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.GetStringAsync($"/api/worlds/{w.Id}/players/{p.Id}/history")));
        await check.ServiceProvider.GetRequiredService<ElifootDbContext>().Entry(record).ReloadAsync();
        Assert.Equal(snapshotBefore, record.SnapshotJson);
    }

    [Fact]
    public async Task ConcurrentSeasonAdvanceAndReconnectLeaveSettledLegacyStrengthHistoryUntouched()
    {
        var w = WorldSeedFactory.CreateSeededWorld(seed: 199812);
        var p = w.Players[0];
        p.Strength = 1; p.SeasonGoals = 6;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        w.SeasonNumber = 2;
        var historicalChange = Assert.Single(w.PlayerStrengthHistory, e => e.PlayerId == p.Id);
        w.RoundNumber = w.TotalRoundsInSeason;
        w.RoundState = RoundState.RESULTS;
        var repo = new InMemoryWorldRepository();
        await repo.SaveWorldAsync(w);
        var orchestrator = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => orchestrator.AdvanceToNextRoundPreparationAsync(w.Id)));
        Assert.Equal(3, w.SeasonNumber);
        Assert.Equal(2, p.Strength);
        Assert.Equal(historicalChange, Assert.Single(w.PlayerStrengthHistory, e => e.PlayerId == p.Id));
        var loaded = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        await repo.SaveWorldAsync(loaded);
        var queries = new PlayerRankingsApplicationService(repo);
        var replies = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            queries.ReadWorldAsync(w.Id, world => PlayerStatisticsEngine.GetHistory(world, p.Id)!)));
        Assert.All(replies, reply =>
        {
            Assert.Equal(2, reply!.Player.Strength);
            Assert.Equal(historicalChange, Assert.Single(reply.StrengthHistory));
        });
        Assert.Equal(1, loaded.EvolvedPlayerStrengthSeasons.Single());
    }
}
