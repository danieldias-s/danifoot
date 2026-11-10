using Elifoot.Domain;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

public class FinancialPersistence007Tests
{
    [Fact]
    public async Task AiOfferSubmissionAndHumanAcceptance_HaveDistinctMetricsAndCorrectAttribution()
    {
        var world=WorldSeedFactory.CreateSeededWorld();var buyer=world.Clubs[0];buyer.CashBalance=20_000_000;
        foreach(var p in world.Players.Where(p=>p.ClubId==buyer.Id))p.Strength=15;
        foreach(var p in world.Players) { p.IsListedForTransfer=false;p.AskingPrice=null; }
        world.Players.Last(p=>p.ClubId==buyer.Id && p.Position!=PlayerPosition.G).ClubId=null;
        foreach(var c in world.Clubs.Where(c=>c.Id!=buyer.Id))
        {
            var coach=new HumanCoach { CurrentClubId=c.Id,Nickname="Controlled seller" };
            c.HumanCoachId=coach.Id;world.HumanCoaches.Add(coach);
        }
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(world);
        var offer=Assert.Single(world.TransferOffers);
        var before=AiMarketMetrics.FromRecords(buyer.AiManagerId!.Value,world.Transactions,world.AuditLogs,world.TransferOffers);
        Assert.Equal(1,before.AiOffersSubmitted);Assert.Equal(0,before.AiPurchasesCompleted);
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(world);
        var service=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        var seller=world.Clubs.Single(c=>c.Id==offer.SellerClubId);
        var result=await service.RespondToTransferOfferAsync(world.Id,seller.HumanCoachId!.Value,offer.Id,true,"007-ai-human-offer",salaryTermsToken:MarketContractTermsEngine.SalaryTermsToken(world,world.Players.Single(x=>x.Id==offer.PlayerId)));
        Assert.True(result.Success,result.Error);
        var after=AiMarketMetrics.FromRecords(buyer.AiManagerId.Value,world.Transactions,world.AuditLogs,world.TransferOffers);
        Assert.Equal(1,after.AiOffersSubmitted);Assert.Equal(1,after.AiOffersAccepted);Assert.Equal(1,after.AiPurchasesCompleted);
        Assert.Equal(before.AiPurchaseAttempts+1,after.AiPurchaseAttempts);
        var tx=Assert.Single(world.Transactions,t=>t.Category==FinancialTransactionCategory.TransferPurchase);
        Assert.Equal(buyer.AiManagerId,tx.ManagerId);Assert.Equal("AcceptedOffer",tx.TransactionType);
        Assert.Equal(0m,world.Transactions.Where(t=>t.TransferTransactionId==tx.Id).Sum(t=>t.Amount));
    }

    [Fact]
    public async Task SaveMoreThan200LedgerEntries_PersistsEveryEntryExactlyOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<ElifootDbContext>(o => o.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        var repository = new EfWorldRepository(provider.GetRequiredService<IServiceScopeFactory>());
        var world = WorldSeedFactory.CreateSeededWorld();
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, []); // 128 initial + 128 sponsorship entries.
        Assert.True(world.Transactions.Count > 200);
        await repository.SaveWorldAsync(world);
        using (var firstCheck = provider.CreateScope())
            Assert.Equal(world.Transactions.Count, await firstCheck.ServiceProvider.GetRequiredService<ElifootDbContext>().FinancialLedger.CountAsync(t => t.GameWorldId == world.Id));
        await repository.SaveWorldAsync(world);
        using var check = provider.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ElifootDbContext>();
        Assert.Equal(world.Transactions.Count, await db.FinancialLedger.CountAsync(t => t.GameWorldId == world.Id));
        var snapshot = await db.Worlds.SingleAsync(t => t.Id == world.Id);
        var restored = System.Text.Json.JsonSerializer.Deserialize<GameWorld>(snapshot.SnapshotJson,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var count = restored.Transactions.Count;
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored, []);
        Assert.Equal(count, restored.Transactions.Count);
    }
}
