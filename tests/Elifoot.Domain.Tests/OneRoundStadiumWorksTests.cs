using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;

public class OneRoundStadiumWorksTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewExpansionUsesOldCapacityForThisMatchAndCompletesAtTheRoundEnd(bool plays)
    {
        var w=WorldSeedFactory.CreateSeededWorld();var c=w.Clubs.First(x=>x.DivisionTier==DivisionTier.QuartaDivisao);
        c.CashBalance=10_000_000;c.Stadium.BuiltCapacity=10000;c.Stadium.UsableCapacity=9000;
        Assert.True(MarketAndEconomyEngine.StartStadiumExpansion(w,c,5000).Success);
        Assert.Equal(10000,c.Stadium.BuiltCapacity);Assert.Equal(9000,c.Stadium.UsableCapacity);
        Assert.Equal(1,c.Stadium.ExpansionRoundsRemaining);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        c=restored.Clubs.Single(x=>x.Id==c.Id);
        var match=new MatchFixture{GameWorldId=w.Id,SeasonNumber=w.SeasonNumber,RoundNumber=w.RoundNumber,
            CompetitionType=CompetitionType.League,HomeClubId=c.Id,AwayClubId=w.Clubs[0].Id,IsFinished=true,CurrentMinute=90};
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored,plays?[match]:[]);
        if(plays)Assert.InRange(match.Attendance,0,9000);
        Assert.Equal(15000,c.Stadium.BuiltCapacity);Assert.Equal(14000,c.Stadium.UsableCapacity);
        Assert.Equal(0,c.Stadium.ExpansionRoundsRemaining);
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored,plays?[match]:[]);
        Assert.Equal(15000,c.Stadium.BuiltCapacity);
        Assert.Single(restored.Transactions,t=>t.ClubId==c.Id&&t.Category==FinancialTransactionCategory.StadiumExpansion);
    }

    [Fact]
    public void NewRenovationCompletesAfterOneGlobalRoundWithoutChangingCurrentMatchConditions()
    {
        var w=WorldSeedFactory.CreateSeededWorld();var c=w.Clubs[0];c.CashBalance=10_000_000;
        c.Stadium.ConditionPercent=70;c.Stadium.BuiltCapacity=10000;c.Stadium.UsableCapacity=7000;
        Assert.True(MarketAndEconomyEngine.StartStadiumRenovation(w,c).Success);
        Assert.Equal(70,c.Stadium.ConditionPercent);Assert.Equal(7000,c.Stadium.UsableCapacity);
        Assert.Equal(1,c.Stadium.RenovationRoundsRemaining);
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[]);
        Assert.Equal(85,c.Stadium.ConditionPercent);Assert.Equal(8500,c.Stadium.UsableCapacity);
        Assert.Equal(0,c.Stadium.RenovationRoundsRemaining);
    }

    [Fact]
    public void ExistingWorksKeepTheirRemainingDurationAndMatchBasedProgress()
    {
        var w=WorldSeedFactory.CreateSeededWorld();var c=w.Clubs[0];
        c.Stadium.ExpansionRoundsRemaining=5;c.Stadium.ExpansionTargetSeats=5000;c.Stadium.RenovationRoundsRemaining=3;
        w=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;c=w.Clubs[0];
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[]);
        Assert.Equal(5,c.Stadium.ExpansionRoundsRemaining);Assert.Equal(3,c.Stadium.RenovationRoundsRemaining);
    }
}
