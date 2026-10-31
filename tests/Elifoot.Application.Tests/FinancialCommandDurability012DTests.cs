using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Application.Tests;

public class FinancialCommandDurability012DTests
{
    [Fact]
    public async Task RetryAfterFailedSavePersistsTheOriginalLoanWithoutCreditingItAgain()
    {
        var w=WorldSeedFactory.CreateSeededWorld();var c=w.Clubs[0];var coach=new HumanCoach{CurrentClubId=c.Id};c.HumanCoachId=coach.Id;w.HumanCoaches.Add(coach);
        var repo=new FailsOneSave(w);var service=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);decimal before=c.CashBalance;
        await Assert.ThrowsAsync<IOException>(()=>service.TakeBankLoanAsync(w.Id,coach.Id,500000,0,idempotencyKey:"retry-after-save-error"));
        Assert.Null(repo.DurableSnapshot);Assert.Equal(before+500000,c.CashBalance);Assert.Single(w.BankLoans);
        var retry=await service.TakeBankLoanAsync(w.Id,coach.Id,500000,0,idempotencyKey:"retry-after-save-error",expectedRoundVersion:-1);
        Assert.True(retry.Success,retry.Error);Assert.Equal(2,repo.SaveCalls);Assert.NotNull(repo.DurableSnapshot);
        var persisted=JsonSerializer.Deserialize<GameWorld>(repo.DurableSnapshot!)!;
        Assert.Equal(before+500000,persisted.Clubs[0].CashBalance);Assert.Single(persisted.BankLoans);Assert.NotEmpty(persisted.Economy.Commands);
        Assert.False((await service.TakeBankLoanAsync(w.Id,coach.Id,100000,0,idempotencyKey:"retry-after-save-error")).Success);
        Assert.Equal(before+500000,c.CashBalance);
    }
    [Fact]
    public async Task VoluntaryAuctionRetryAfterFailedSavePersistsOneAuctionAndItsOriginalDeadline()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.Auctions.Clear();w.RoundNumber=7;
        var c=w.Clubs[0];var coach=new HumanCoach{CurrentClubId=c.Id};c.HumanCoachId=coach.Id;w.HumanCoaches.Add(coach);
        var p=w.Players.Last(p=>p.ClubId==c.Id);var repo=new FailsOneSave(w);var service=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        await Assert.ThrowsAsync<IOException>(()=>service.CreateVoluntaryAuctionAsync(w.Id,coach.Id,p.Id,idempotencyKey:"retry-auction"));
        var first=Assert.Single(w.Auctions);Assert.Equal(8,first.ExpiresAtRound);Assert.Null(repo.DurableSnapshot);
        // Reconnection with an old room version replays the accepted command, even after a state transition.
        w.RoundState=RoundState.RUNNING_FIRST_HALF;
        var result=await service.CreateVoluntaryAuctionAsync(w.Id,coach.Id,p.Id,idempotencyKey:"retry-auction",expectedRoundVersion:-1);
        Assert.True(result.Success,result.Error);Assert.Equal(first.Id,result.AuctionId);Assert.Single(w.Auctions);Assert.Equal(2,repo.SaveCalls);
        var durable=JsonSerializer.Deserialize<GameWorld>(repo.DurableSnapshot!)!;Assert.Equal(8,durable.Auctions.Single().ExpiresAtRound);
        Assert.False((await service.CreateVoluntaryAuctionAsync(w.Id,coach.Id,p.Id,0,3,idempotencyKey:"retry-auction")).Success);
        Assert.Equal(c.Id,p.ClubId);
    }
    private sealed class FailsOneSave(GameWorld world):IWorldRepository
    {
        public int SaveCalls {get;private set;}
        public string? DurableSnapshot {get;private set;}
        public Task<GameWorld?> GetWorldAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<GameWorld?>(id==world.Id?world:null);
        public Task<GameWorld?> GetWorldByInviteCodeAsync(string invite,CancellationToken ct=default)=>Task.FromResult<GameWorld?>(world);
        public Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct=default)=>Task.FromResult(new List<GameWorld>{world});
        public Task SaveWorldAsync(GameWorld value,CancellationToken ct=default)
        {
            SaveCalls++;if(SaveCalls==1)throw new IOException("Controlled persistence failure.");
            DurableSnapshot=JsonSerializer.Serialize(value);return Task.CompletedTask;
        }
    }
}
