using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;

public sealed class PlayableWorld013CTests
{
    private static readonly List<string> Countries=["BR","AR","GB","ES","DE","IT","FR","PT"];
    private static readonly DateTime Observed=new(2026,10,30,0,0,0,DateTimeKind.Utc);
    private static CatalogSelection Selection(FootballGender gender,int clubs=128,int players=0)
    {
        string[] positions=["G","D","D","D","D","M","M","M","M","A","A"];
        var teams=Enumerable.Range(0,clubs).Select(i=>{
            string id=$"test-{gender}-{i}";string country=Countries[i%8];
            var athletes=Enumerable.Range(0,players).Select(j=>new CatalogAthlete($"{id}-p{j}",id,"synthetic013c",gender,"2026",
                $"Synthetic test athlete {i}-{j}",positions[j%11],25,"synthetic013c",Observed,$"{id}-p{j}",CatalogDataOrigin.Real,
                MembershipStart:new(2020,1,1),MembershipEnd:new(2030,12,31),
                MembershipEvidence:new(CatalogMembershipStatus.DatedActive,$"test-statement-{i}-{j}","Synthetic test evidence, not a real contract",new(2026,10,30)))).ToList();
            return new EligibleCatalogClub(WorldCreatorEngine.ClubKey(country,gender,"synthetic013c",id),id,"synthetic013c",
                $"Synthetic test club {i}",$"T{i}",country,country,gender,"Synthetic test league","2026","#006400","#FFFFFF",i+1,athletes,[],
                RankingInfo:new(id,"synthetic013c",gender,"2026",i+1,"synthetic ranking",Observed,1800-i*4,"derived-elo-v1",.8,"test-comparable",CatalogDataOrigin.Estimated),
                Verification:new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,"Synthetic test verification",Observed,"2026",true));
        }).ToList();return new(gender,"2026",Countries,teams,[],[]);
    }
    private static WorldCreatorRequest Request(FootballGender gender,WorldDataMode mode,bool players=false,bool teams=false)
        =>new("Synthetic 013C acceptance",gender,"2026",Countries,WorldAssemblyMode.Automatic,DistrictFillMode.Random,130013,[],
            AllowFictionalTeams:teams,UseSavedData:true,AllowFictionalPlayers:players,DataMode:mode);

    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public void CompleteRealModeRequiresCurrentEvidenceAndNeverCompletesAutomatically(FootballGender gender)
    {
        var selection=Selection(gender);var request=Request(gender,WorldDataMode.REAL_COMPLETE);
        var preview=WorldCreatorEngine.Preview(request,selection);
        Assert.NotEmpty(preview.Errors);Assert.Empty(preview.Clubs);Assert.Equal(128,preview.PendingSlots);
        Assert.Equal(0,preview.DataSummary!.GeneratedPlayers);Assert.Equal(128,preview.DataSummary.VerifiedRealClubs);
        Assert.Equal(0,preview.DataSummary.CompleteRealSquads);Assert.Throws<ArgumentException>(()=>WorldCreatorEngine.Create(request,preview));
        var incompatible=WorldCreatorEngine.Preview(request with {AllowFictionalPlayers=true,AllowFictionalTeams=true},selection);
        Assert.NotEmpty(incompatible.Errors);Assert.Empty(incompatible.Clubs);
        var complete=WorldCreatorEngine.Preview(request,Selection(gender,players:11));Assert.Empty(complete.Errors);
        var world=WorldCreatorEngine.Create(request,complete);Assert.Equal(1408,world.Players.Count);
        Assert.All(world.Players,p=>Assert.Equal(WorldPlayerOrigin.REAL_VERIFIED_CURRENT,p.CatalogIdentity!.Origin));
    }
    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public void ExplicitCompletionBuildsVariedDeterministicSquadsAndPreservesProvenance(FootballGender gender)
    {
        var selection=Selection(gender);var request=Request(gender,WorldDataMode.REAL_CLUBS_SIMULATED,true);
        var a=WorldCreatorEngine.Preview(request,selection);var b=WorldCreatorEngine.Preview(request,selection);
        Assert.Empty(a.Errors);Assert.Equal(128,a.Clubs.Count);Assert.Equal(2304,a.DataSummary!.GeneratedPlayers);
        Assert.Equal(a.Clubs.SelectMany(c=>c.Club.Players).Select(p=>(p.CanonicalPlayerId,p.Strength)),b.Clubs.SelectMany(c=>c.Club.Players).Select(p=>(p.CanonicalPlayerId,p.Strength)));
        Assert.All(a.Clubs,c=>{Assert.Equal(18,c.Club.Players.Count);Assert.True(c.Club.Players.Select(p=>p.Strength).Distinct().Count()>3);
            Assert.Equal(2,c.Club.Players.Count(p=>p.Position=="G"));Assert.True(c.Club.Players.Count(p=>p.Position!="G")>=10);});
        Assert.True(a.Clubs.First().Club.Players.Average(p=>p.Strength)>a.Clubs.Last().Club.Players.Average(p=>p.Strength));
        var world=WorldCreatorEngine.Create(request,a);Assert.Equal(ForceEvolutionMode.ClassicAdapted,world.ForceEvolutionMode);
        Assert.Equal(OperatingEconomyEngine.CurrentPolicy,world.Economy.OperatingRules.Name);Assert.Equal(138800000,world.Clubs.Sum(c=>c.CashBalance));
        Assert.All(world.Players,p=>{Assert.InRange(p.Strength,1,50);Assert.True(p.SalaryPerRound>0);Assert.True(p.ContractRoundsRemaining>0);
            Assert.True(Enum.IsDefined(p.Behavior));Assert.Equal(WorldPlayerOrigin.FICTIONAL_GENERATED,p.CatalogIdentity!.Origin);Assert.Equal(request.Seed,p.CatalogIdentity.GeneratedSeed);});
        var loaded=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        Assert.Equal(request.DataMode,loaded.CatalogOrigin!.DataMode);Assert.Equal(request.Seed,loaded.CatalogOrigin.GeneratedPlayerSeed);
        Assert.Equal(world.Players.Select(p=>p.CatalogIdentity!.CanonicalIdentity),loaded.Players.Select(p=>p.CatalogIdentity!.CanonicalIdentity));
        var package=WorldEditorAndPackageService.ExportWorldToPackage(world);var clone=WorldEditorAndPackageService.CreateWorldFromPackage(package,"Provenance copy").World!;
        Assert.Equal(world.Players.Select(p=>p.CatalogIdentity!.Origin),clone.Players.Select(p=>p.CatalogIdentity!.Origin));
        var csv=WorldEditorAndPackageService.ExportPlayersToCsv(world);var parsed=WorldEditorAndPackageService.ParsePlayersFromCsv(csv);
        Assert.Empty(parsed.ParseErrors);Assert.Equal(world.Players.Select(p=>p.CatalogIdentity!.CanonicalIdentity),parsed.Players.Select(p=>p.CatalogIdentity!.CanonicalIdentity));
        Assert.Throws<ArgumentException>(()=>WorldCreatorEngine.Create(request with {AllowFictionalPlayers=false},a));
    }
    [Fact]
    public async Task FullButUnbalancedRosterCompletesWithoutReplacingThePositionBeingAdded()
    {
        var club=Selection(FootballGender.Male,1,24).Clubs[0];
        club=club with {Players=club.Players.Select(p=>p with {Position="M",Strength=50,PlayerOrigin=WorldPlayerOrigin.REAL_VERIFIED_CURRENT}).ToList()};
        // A former replacement could repeatedly remove the newly added defender at the 24-player limit.
        var completed=await Task.Run(()=>PlayableWorldPolicy.Prepare(club,130013,true)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(24,completed.Players.Count);Assert.True(PlayableWorldPolicy.Complete(completed.Players));
        Assert.Contains(completed.Players,p=>p.Position=="A");Assert.Equal(24,completed.Players.Select(p=>p.CanonicalPlayerId).Distinct().Count());
        Assert.Equal(0,completed.Players.Count(p=>p.PlayerOrigin==WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE));
    }
    [Fact]
    public void RefusalDoesNotGeneratePlayersOrFictionalTeams()
    {
        var request=Request(FootballGender.Male,WorldDataMode.REAL_CLUBS_SIMULATED);
        var preview=WorldCreatorEngine.Preview(request,Selection(FootballGender.Male));Assert.NotEmpty(preview.Errors);
        Assert.Empty(preview.Clubs.SelectMany(c=>c.Club.Players));Assert.Equal(0,preview.DataSummary!.GeneratedPlayers);
        var c=WorldCreatorEngine.Preview(request with {DataMode=WorldDataMode.FREE_WORLD,AllowFictionalTeams=true},Selection(FootballGender.Male,2));
        Assert.NotEmpty(c.Errors);Assert.Equal(2,c.Clubs.Count);Assert.Empty(c.Clubs.SelectMany(c=>c.Club.Players));
    }
    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public void OneCountryFreeWorldNeedsConsentAndNeverDuplicatesIdentities(FootballGender gender)
    {
        var request=Request(gender,WorldDataMode.FREE_WORLD,true,true) with {Countries=["BR"]};
        var selection=Selection(gender,8);var preview=WorldCreatorEngine.Preview(request,selection);
        Assert.Empty(preview.Errors);Assert.Equal(128,preview.Clubs.Count);Assert.Equal(1,preview.DataSummary!.SelectedRealClubs);
        Assert.Equal(127,preview.DataSummary.SelectedFictionalClubs);Assert.All(preview.Clubs,c=>Assert.Equal("BR",c.Club.CountryCode));
        Assert.Equal(128,preview.Clubs.Select(c=>c.Club.Key).Distinct().Count());
        Assert.Equal(2304,preview.Clubs.SelectMany(c=>c.Club.Players).Select(p=>p.CanonicalPlayerId).Distinct().Count());
        Assert.NotEmpty(WorldCreatorEngine.Preview(request with {AllowFictionalTeams=false},selection).Errors);
        Assert.NotEmpty(WorldCreatorEngine.Preview(request with {Gender=gender==FootballGender.Male?FootballGender.Female:FootballGender.Male,DataMode=WorldDataMode.REAL_CLUBS_SIMULATED,AllowFictionalTeams=false},selection).Errors);
    }
    [Theory][InlineData("historical")][InlineData("open")][InlineData("missing")][InlineData("wrong-club")][InlineData("wrong-gender")][InlineData("forged-label")]
    public void HistoricalAndUnprovenMembershipsNeverEnterTheInitialRealRoster(string issue)
    {
        var s=Selection(FootballGender.Female);var club=s.Clubs[0];var p=Selection(FootballGender.Female,1,1).Clubs[0].Players[0];
        p=issue switch{
            "historical"=>p with {MembershipEnd=new(2025,1,1),MembershipEvidence=p.MembershipEvidence! with {Status=CatalogMembershipStatus.Historical}},
            "open"=>p with {MembershipEnd=null,MembershipEvidence=p.MembershipEvidence! with {Status=CatalogMembershipStatus.OpenEndedUnconfirmed}},
            "missing"=>p with {MembershipEvidence=null},"wrong-club"=>p with {ClubExternalId="another-team"},
            "wrong-gender"=>p with {Gender=FootballGender.Male},"forged-label"=>p with {MembershipEnd=null},_=>p};
        s.Clubs[0]=club with {Players=[p]};var result=WorldCreatorEngine.Preview(Request(FootballGender.Female,WorldDataMode.REAL_CLUBS_SIMULATED,true),s);
        Assert.Empty(result.Errors);Assert.Equal(0,result.DataSummary!.RealCurrentPlayers);
        Assert.DoesNotContain(result.Clubs.SelectMany(c=>c.Club.Players),x=>x.CanonicalPlayerId==p.CanonicalPlayerId);
        Assert.Contains(result.Clubs[0].Club.ReferencePlayers!,x=>x.PlayerOrigin==WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void VerifiedPlayerIsKeptOnceAndManualPlacementDoesNotChangeStrength(bool catalogRules)
    {
        var s=Selection(FootballGender.Female);s.Clubs[0]=s.Clubs[0] with {Players=Selection(FootballGender.Female,1,1).Clubs[0].Players};
        var request=Request(FootballGender.Female,WorldDataMode.REAL_CLUBS_SIMULATED,true) with {UseWorldCatalogRules=catalogRules};
        if(catalogRules)s=s with {Clubs=WorldCatalogRules016B.Classify(s.Clubs)};
        var a=WorldCreatorEngine.Preview(request,s);
        Assert.Equal(1,a.DataSummary!.RealCurrentPlayers);
        Assert.Equal(a.Clubs.Sum(c=>c.Club.Players.Count)-1,a.DataSummary.GeneratedPlayers);
        var assignments=a.Clubs.Select(c=>new ClubAssignment(c.Club.Key,c.Division)).ToList();
        assignments[0]=assignments[0] with {Division=4};assignments[24]=assignments[24] with {Division=1};
        var manual=WorldCreatorEngine.Preview(request with {Mode=WorldAssemblyMode.Custom,DistrictMode=DistrictFillMode.Manual,Assignments=assignments,FillRemainingDivisions=false},s);
        Assert.Empty(manual.Errors);Assert.Equal(96,manual.Clubs.Count(c=>c.Division==5));
        foreach(var c in a.Clubs)Assert.Equal(c.Club.Players.Select(p=>(p.CanonicalPlayerId,p.Strength)),manual.Clubs.Single(m=>m.Club.Key==c.Club.Key).Club.Players.Select(p=>(p.CanonicalPlayerId,p.Strength)));
        var elite=WorldCreatorEngine.Create(request with {Mode=WorldAssemblyMode.Custom},manual);
        var moved=Assert.Single(elite.Clubs,c=>c.Name==a.Clubs[0].Club.Name);
        Assert.Equal(DivisionTier.QuartaDivisao,moved.DivisionTier);
        Assert.Contains(elite.Players,p=>p.ClubId==moved.Id&&p.Strength>34);
    }
    [Fact]
    public void ProvisionalOrderingUsesSavedSeedAndIsNeverPresentedAsSportingRanking()
    {
        var s=Selection(FootballGender.Male);for(int i=0;i<s.Clubs.Count;i++)s.Clubs[i]=s.Clubs[i] with {RankingInfo=null,Rank=null};
        var request=Request(FootballGender.Male,WorldDataMode.REAL_CLUBS_SIMULATED,true);var a=WorldCreatorEngine.Preview(request,s);
        var b=WorldCreatorEngine.Preview(request,s);var different=WorldCreatorEngine.Preview(request with {Seed=1},s);
        Assert.Equal(a.Clubs.Select(c=>c.Club.Key),b.Clubs.Select(c=>c.Club.Key));Assert.NotEqual(a.Clubs.Select(c=>c.Club.Key),different.Clubs.Select(c=>c.Club.Key));
        Assert.All(a.Clubs,c=>{Assert.Equal("ProvisionalSeededOrder-v1",c.Club.RankingInfo!.Method);Assert.Equal(0,c.Club.RankingInfo.Confidence);});
        Assert.Contains(a.Warnings,w=>w.Contains("baixa confiança"));
    }
    [Theory]
    [InlineData("REAL_VERIFIED_CURRENT")][InlineData("REAL_CURRENT_VERIFIED")]
    public void CurrentOriginAcceptsLegacyAndCanonicalNames(string persisted)
    {
        var origin=JsonSerializer.Deserialize<WorldPlayerOrigin>("\""+persisted+"\"");
        Assert.Equal(WorldPlayerOrigin.REAL_CURRENT_VERIFIED,origin);
        Assert.Equal("\"REAL_CURRENT_VERIFIED\"",JsonSerializer.Serialize(origin));
    }
    [Fact]
    public void NewGameGeneratedPlayersAcquireStableFictionalOriginWithoutChangingContracts()
    {
        var request=Request(FootballGender.Female,WorldDataMode.REAL_CLUBS_SIMULATED,true);
        var world=WorldCreatorEngine.Create(request,WorldCreatorEngine.Preview(request,Selection(FootballGender.Female)));
        var initial=world.Players[0];var original=initial.CatalogIdentity;
        var generated=new Player{Id=Guid.NewGuid(),GameWorldId=world.Id,ClubId=world.Clubs[0].Id,Name="Atleta do jogo",Position=PlayerPosition.M,
            Strength=24,SalaryPerRound=12345.67m,ContractRoundsRemaining=19};
        world.Players.Add(generated);world.SeasonNumber=2;RuntimePlayerProvenance.EnsureGeneratedPlayers(world);
        Assert.Equal(WorldPlayerOrigin.FICTIONAL_GENERATED,generated.CatalogIdentity!.Origin);
        Assert.Equal(2,generated.CatalogIdentity.CreatedInGameSeason);Assert.Equal(12345.67m,generated.SalaryPerRound);Assert.Equal(19,generated.ContractRoundsRemaining);
        var recorded=generated.CatalogIdentity;RuntimePlayerProvenance.EnsureGeneratedPlayers(world);Assert.Same(recorded,generated.CatalogIdentity);Assert.Same(original,initial.CatalogIdentity);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        Assert.Equal(recorded.CanonicalIdentity,restored.Players.Single(p=>p.Id==generated.Id).CatalogIdentity!.CanonicalIdentity);
        var legacy=WorldSeedFactory.CreateSeededWorld();RuntimePlayerProvenance.EnsureGeneratedPlayers(legacy);Assert.All(legacy.Players,p=>Assert.Null(p.CatalogIdentity));
    }
    [Fact]
    public void CanonicalCurrentPlayerDuplicatesAreRejected()
    {
        var s=Selection(FootballGender.Male,players:11);s.Clubs[1].Players[1]=s.Clubs[1].Players[1] with {CanonicalPlayerId=s.Clubs[0].Players[1].CanonicalPlayerId};
        Assert.Contains(WorldCreatorEngine.Preview(Request(FootballGender.Male,WorldDataMode.REAL_COMPLETE),s).Errors,e=>e.Contains("duplicados"));
    }
}
