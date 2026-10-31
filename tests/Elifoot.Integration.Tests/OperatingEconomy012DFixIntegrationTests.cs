using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
namespace Elifoot.Integration.Tests;

public class OperatingEconomy012DFixIntegrationTests
{
    [Theory][InlineData(2)][InlineData(4)][InlineData(6)][InlineData(8)]
    public async Task ReplayedSettlementAndLoanCommandsShareTheWorldGateWithoutExtraCommercialIncome(int participants)
    {
        var world=WorldSeedFactory.CreateSeededWorld();var club=world.Clubs[0];var coach=new HumanCoach{CurrentClubId=club.Id};
        world.HumanCoaches.Add(coach);club.HumanCoachId=coach.Id;
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(world);
        var service=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        decimal before=club.CashBalance;
        async Task Settle()
        {
            var gate=MultiplayerRoundOrchestrator.GetRoomLock(world.Id);await gate.WaitAsync();
            try{MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world,[]);await repo.SaveWorldAsync(world);}finally{gate.Release();}
        }
        var settlements=Enumerable.Range(0,participants).Select(_=>Settle());
        var commands=Enumerable.Range(0,participants).Select(_=>service.TakeBankLoanAsync(world.Id,coach.Id,500000,0,idempotencyKey:"same-bank-command"));
        await Task.WhenAll(settlements.Cast<Task>().Concat(commands));
        Assert.Single(world.BankLoans);Assert.Equal(before+500000+8409.09m,club.CashBalance);
        Assert.Single(world.Transactions,t=>t.ClubId==club.Id&&t.Category==FinancialTransactionCategory.Sponsorship);
        Assert.Single(world.Transactions,t=>t.ClubId==club.Id&&t.Category==FinancialTransactionCategory.LoanDisbursement);
        Assert.Equal(0,world.BankLoans[0].InterestCharged);Assert.Equal(500000,world.BankLoans[0].RemainingBalance);
        var restored=System.Text.Json.JsonSerializer.Deserialize<GameWorld>(System.Text.Json.JsonSerializer.Serialize(world))!;
        decimal cash=restored.Clubs[0].CashBalance;MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored,[]);Assert.Equal(cash,restored.Clubs[0].CashBalance);
    }
}
