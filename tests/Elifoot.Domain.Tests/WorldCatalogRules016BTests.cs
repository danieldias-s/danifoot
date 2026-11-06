using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;
public class WorldCatalogRules016BTests
{
    private static EligibleCatalogClub Club(int n,int count=30) {
        string id="club-"+n.ToString("D3");
        var players=Enumerable.Range(0,count).Select(i=>new CatalogAthlete(id+"-p"+i,id,"test",FootballGender.Male,"2026",
            "Fixture player "+i,i<3?"G":i<12?"D":i<21?"M":"A",20+i%15,"test",DateTime.UnixEpoch,
            Performance:i%3==0?null:new(Rating:i%10,Minutes:i*10,Appearances:i))).ToList();
        return new(id,id,"test",id,id,"ES","Espanha",FootballGender.Male,"Fixture league","2026","#000000","#FFFFFF",n+1,
            players,[],RankingInfo:new(id,"test",FootballGender.Male,"2026",n+1,"fixture",DateTime.UnixEpoch,2000-n,Confidence:.5));
    }
    [Fact] public void GlobalPowerUsesPairsKeepsDetailedOrderAndMinimumBeyondForty() {
        var input=Enumerable.Range(0,64).Select(n=>Club(n)).Reverse().ToList();
        var ranked=WorldCatalogRules016B.Classify(input);
        Assert.Equal(Enumerable.Range(0,64).Select(n=>Math.Max(1,20-n/2)),ranked.Select(c=>c.InitialProfile!.ClubInitialPower));
        Assert.Equal(Enumerable.Range(1,64),ranked.Select(c=>c.InitialProfile!.WorldRank));
        Assert.Equal(ranked.Select(c=>c.Key),WorldCatalogRules016B.Classify(input.AsEnumerable().Reverse()).Select(c=>c.Key));
    }
    [Fact] public void ManualTiesRemainAndAutomaticOrderUsesScore() {
        var clubs=WorldCatalogRules016B.Classify(Enumerable.Range(0,24).Select(n=>Club(n)));
        clubs=clubs.Select(c=>c with {InitialProfile=c.InitialProfile! with {ClubInitialPower=20,ManualPower=true}}).ToList();
        var ranked=WorldCatalogRules016B.Classify(clubs);
        Assert.All(ranked,c=>Assert.Equal(20,c.InitialProfile!.ClubInitialPower));
        Assert.Equal(clubs.Select(c=>c.Key),WorldCatalogRules016B.Order(ranked.AsEnumerable().Reverse()).Select(c=>c.Key));
        Assert.Equal(8,ranked.Take(8).Count());
    }
    [Fact] public void SquadCycleRepeatsAfterFourteenAndEliteHasPriority() {
        int[] expected=[23,23,22,22,21,21,20,20,19,19,18,18,17,17,23,23];
        Assert.Equal(expected,Enumerable.Range(0,16).Select(WorldCatalogRules016B.SquadSizeAt));
        var club=WorldCatalogRules016B.Classify([Club(0)]).Single();
        var prepared=WorldCatalogRules016B.Prepare(club,13,1998,false,20);
        Assert.Equal(23,prepared.Players.Count);Assert.True(prepared.InitialProfile!.EliteRuleApplied);
        Assert.Equal(17,WorldCatalogRules016B.Prepare(club,13,1998,false,0).Players.Count);
    }
    [Theory][InlineData(17)][InlineData(18)][InlineData(19)][InlineData(20)][InlineData(21)][InlineData(22)][InlineData(23)]
    public void SelectionBalancesPositionsDeduplicatesAndDoesNotChangeStrength(int size) {
        var club=Club(1);var input=club.Players.Concat(club.Players.Take(3)).ToList();
        var selected=WorldCatalogRules016B.SelectRoster(input,size);
        Assert.Equal(size,selected.Count);Assert.Equal(size,selected.Select(p=>p.PlayerExternalId).Distinct().Count());
        Assert.True(selected.Count(p=>p.Position=="G")>=2);Assert.True(selected.Count(p=>p.Position!="G")>=10);
        Assert.All(new[]{"D","M","A"},p=>Assert.Contains(selected,x=>x.Position==p));
        Assert.All(selected,p=>Assert.Equal(club.Players.Single(x=>x.PlayerExternalId==p.PlayerExternalId).Strength,p.Strength));
        Assert.Equal(selected.Select(p=>p.PlayerExternalId),WorldCatalogRules016B.SelectRoster(input.AsEnumerable().Reverse(),size).Select(p=>p.PlayerExternalId));
    }
    [Fact] public void MissingPlayersRequireExplicitConsentAndGeneratedIdsStayUnique() {
        var club=Club(0,0);
        Assert.Empty(WorldCatalogRules016B.Prepare(club,0,1998,false,0).Players);
        var partial=PlayableWorldPolicy.Prepare(club,1998,true);
        var selected=WorldCatalogRules016B.Prepare(partial,0,1998,true,0);
        Assert.Equal(23,selected.Players.Count);Assert.Equal(23,selected.Players.Select(p=>p.PlayerExternalId).Distinct().Count());
        Assert.All(selected.Players,p=>Assert.Equal(WorldPlayerOrigin.FICTIONAL_GENERATED,p.PlayerOrigin));
        Assert.All(selected.Players,p=>Assert.InRange(p.Strength,1,50));
    }

    [Fact]
    public void OrderedDivisionSwapsKeepRankedSquadsAndRecomputeEligibleDistricts()
    {
        var clubs = WorldCatalogRules016B.Classify(Enumerable.Range(0, 130).Reverse().Select(n => Club(n, 0) with
        {
            Verification = new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED, "Test catalog", DateTime.UnixEpoch)
        }));
        var request = new WorldCreatorRequest("Ordenação manual", FootballGender.Male, "2026", ["ES"],
            WorldAssemblyMode.Automatic, DistrictFillMode.Ranking, 1998, [], MaxHumans: 1, AllowFictionalPlayers: true,
            DataMode: WorldDataMode.REAL_CLUBS_SIMULATED, UseWorldCatalogRules: true, PreserveRankedSquadStrengths: true);
        var selection = new CatalogSelection(request.Gender, request.Season, request.Countries, clubs, [], []);
        var automatic = WorldCreatorEngine.Preview(request, selection);
        var original = WorldCreatorEngine.Preview(request with { PreserveRankedSquadStrengths = false }, selection);
        Assert.Empty(automatic.Errors);
        Assert.Equal(clubs.Take(32).Select(c => c.Key), automatic.Clubs.Take(32).Select(c => c.Club.Key));
        Assert.Equal(original.Clubs.SelectMany(c => c.Club.Players).Select(p => p.Strength),
            automatic.Clubs.SelectMany(c => c.Club.Players).Select(p => p.Strength));

        var slots = automatic.Clubs.Take(32).Select(c => c.Club.Key).ToArray();
        (slots[0], slots[7]) = (slots[7], slots[0]);
        (slots[1], slots[8]) = (slots[8], slots[1]);
        var released = slots[15];
        slots[15] = automatic.Clubs[32].Club.Key;
        var plan = request with
        {
            Mode = WorldAssemblyMode.Custom,
            Assignments = slots.Select((key, index) => new ClubAssignment(key, index / 8 + 1)).ToList()
        };
        var preview = WorldCreatorEngine.Preview(plan, selection);
        Assert.Empty(preview.Errors);
        Assert.Equal(new[] { 8, 8, 8, 8, 96 }, preview.Clubs.GroupBy(c => c.Division).Select(group => group.Count()));
        Assert.Equal(128, preview.Clubs.Select(c => c.Club.Key).Distinct().Count());
        Assert.Equal(slots, preview.Clubs.Take(32).Select(c => c.Club.Key));
        Assert.Contains(preview.Clubs, c => c.Club.Key == released && c.Division == 5);
        var beforeByKey = automatic.Clubs.ToDictionary(c => c.Club.Key);
        Assert.All(preview.Clubs, c => Assert.Equal(beforeByKey[c.Club.Key].Club.Players.Select(p => (p.CanonicalPlayerId, p.Strength)),
            c.Club.Players.Select(p => (p.CanonicalPlayerId, p.Strength))));

        var world = WorldCreatorEngine.Create(plan, preview);
        Assert.Equal(slots, world.CatalogOrigin!.Clubs.Take(32).Select(c => c.Club.Key));
        Assert.Equal(preview.Clubs.SelectMany(c => c.Club.Players).Select(p => p.Strength), world.Players.Select(p => p.Strength));
    }

    [Fact] public void FictionalWorldPreviewPersistsVariableRosterAnd128UniqueClubs() {
        var request=new WorldCreatorRequest("Fixture 016B",FootballGender.Male,"2026",["ES"],WorldAssemblyMode.Automatic,DistrictFillMode.Ranking,1998,[],
            AllowFictionalTeams:true,AllowFictionalPlayers:true,DataMode:WorldDataMode.FREE_WORLD,UseWorldCatalogRules:true,FictionalOnly:true);
        var preview=WorldCreatorEngine.Preview(request,new(FootballGender.Male,"2026",["ES"],[],[],[]));
        Assert.Empty(preview.Errors);Assert.Equal(128,preview.Clubs.Count);
        Assert.Equal(128,preview.Clubs.Select(c=>c.Club.Key).Distinct().Count());
        Assert.All(preview.Clubs.Select((c,i)=>(c,i)),x=>Assert.Equal(WorldCatalogRules016B.SquadSizeAt(x.i),x.c.Club.Players.Count));
        var world=WorldCreatorEngine.Create(request,preview);
        Assert.All(world.Clubs,c=>Assert.Equal(c.InitialSquadSize,world.Players.Count(p=>p.ClubId==c.Id)));
        Assert.Equal(96,world.Clubs.Count(c=>c.DivisionTier==DivisionTier.Distrital));
    }
    [Fact] public void CountryLimitAndRealMinimumAreExplained() {
        var request=new WorldCreatorRequest("Fixture",FootballGender.Male,"2026",Enumerable.Range(0,9).Select(i=>"C"+i).ToList(),WorldAssemblyMode.Automatic,DistrictFillMode.Ranking,1998,[],UseWorldCatalogRules:true);
        var preview=WorldCreatorEngine.Preview(request,new(FootballGender.Male,"2026",request.Countries,[],[],[]));
        Assert.Contains(preview.Errors,e=>e.Contains("países"));Assert.Contains(preview.Errors,e=>e.Contains("128"));
    }
}
