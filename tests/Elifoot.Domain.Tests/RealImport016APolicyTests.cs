using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;
public class RealImport016APolicyTests
{
    private static EligibleCatalogClub Club(FootballGender gender,int count,WorldPlayerOrigin origin=WorldPlayerOrigin.REAL_CURRENT_PROVIDER_REPORTED) {
        var at=DateTime.UtcNow;var asOf=DateOnly.FromDateTime(at);
        var team=new EligibleCatalogClub("TEST:BR:"+gender+":016A","team016a","api-football","FIXTURE club","TEST","BR","Brasil",gender,"TEST league",asOf.Year.ToString(),"#CC0000","#FFFFFF",null,[],[],Verification:new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,"Synthetic policy fixture",at));
        return team with {Players=Enumerable.Range(1,count).Select(i=>new CatalogAthlete("TEST-"+i,team.ExternalId,team.Provider,gender,team.Season,"FIXTURE athlete "+i,i==1?"G":i<5?"D":i<9?"M":"A",25,team.Provider,at,"api-football:TEST-"+i,CatalogDataOrigin.Real,
            new(InitialPlayerStrength.Version+":pending",CatalogDataOrigin.Estimated,.2,"TEST",.5,null),MembershipEvidence:new(CatalogMembershipStatus.ProviderReported,"TEST membership "+i,"Synthetic policy evidence",asOf),PlayerOrigin:origin)).ToList()};
    }
    [Theory][InlineData(FootballGender.Male,11)][InlineData(FootballGender.Female,11)][InlineData(FootballGender.Male,24)][InlineData(FootballGender.Female,26)]
    public void ReportedRosterNeedsExplicitConsentAndPreservesClassificationWhenSelected(FootballGender gender,int count) {
        var club=Club(gender,count);var date=DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Empty(PlayableWorldPolicy.CurrentPlayers(club,date));var permitted=PlayableWorldPolicy.CurrentPlayers(club,date,true);Assert.Equal(count,permitted.Count);Assert.All(permitted,p=>Assert.Equal(WorldPlayerOrigin.REAL_CURRENT_PROVIDER_REPORTED,p.PlayerOrigin));
        var prepared=InitialRosterPolicy.Prepare(club with{Players=permitted},199801,false);Assert.Empty(prepared.Issues);Assert.InRange(prepared.Players.Count,11,24);Assert.Equal(count,club.Players.Count);
        Assert.Contains(prepared.Players,p=>p.Position=="G");Assert.True(prepared.Players.Count(p=>p.Position!="G")>=10);
        var calibrated=InitialPlayerStrength.Apply([club],199801)[0];var same=InitialPlayerStrength.Apply([club],199801)[0];Assert.Equal(calibrated.Players.Select(p=>p.Strength),same.Players.Select(p=>p.Strength));Assert.All(calibrated.Players,p=>{Assert.InRange(p.Strength,1,50);Assert.Equal(CatalogDataOrigin.Estimated,p.StrengthEstimate!.Origin);});
    }
    [Fact] public void StaleReportedMembershipCannotMasqueradeAsCurrentVerifiedOrCurrentReported() {
        var club=Club(FootballGender.Male,11);var date=DateOnly.FromDateTime(DateTime.UtcNow);var old=club.Players.Select(p=>p with {MembershipEvidence=p.MembershipEvidence! with{AsOf=date.AddDays(-8)}}).ToList();
        Assert.Empty(PlayableWorldPolicy.CurrentPlayers(club with{Players=old},date,true));Assert.All(club.Players,p=>Assert.False(PlayableWorldPolicy.Current(p,club,date)));
    }
    [Fact] public void WrongClubAndWrongGenderAndHistoricalCannotEnterCurrentRoster() {
        var club=Club(FootballGender.Male,11);var date=DateOnly.FromDateTime(DateTime.UtcNow);var player=club.Players[0];
        foreach(var wrong in new[]{player with{ClubExternalId="another-team"},player with{Gender=FootballGender.Female},player with{MembershipEnd=date.AddDays(-1)},player with{PlayerOrigin=WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE}})
            Assert.False(PlayableWorldPolicy.ProviderReported(wrong,club,date));
    }
    [Theory][InlineData("REAL_VERIFIED_CURRENT",0)][InlineData("REAL_CURRENT_VERIFIED",0)][InlineData("REAL_HISTORICAL_REFERENCE",1)][InlineData("REAL_HISTORICAL",1)][InlineData("FICTIONAL_GENERATED",2)][InlineData("REAL_CURRENT_PROVIDER_REPORTED",3)]
    public void OriginMigrationReadsLegacyNamesAndPreservesLegacyNumericValues(string text,int number) {
        Assert.Equal((WorldPlayerOrigin)number,JsonSerializer.Deserialize<WorldPlayerOrigin>(JsonSerializer.Serialize(text)));Assert.Equal((WorldPlayerOrigin)number,JsonSerializer.Deserialize<WorldPlayerOrigin>(number.ToString()));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void StableIdentityTransferUsesNewerReportAndEqualDatesRequireReview(bool sameDate) {
        var first=Club(FootballGender.Male,11);var second=first with {Key="second",ExternalId="second",Players=first.Players.Select(p=>p with {ClubExternalId="second",UpdatedAt=sameDate?p.UpdatedAt:p.UpdatedAt.AddMinutes(1)}).ToList()};
        var result=CatalogRosterReconciliation016A.Resolve([first,second]);Assert.Empty(result[0].Players);
        if(sameDate)Assert.Empty(result[1].Players);else Assert.Equal(11,result[1].Players.Count);
        Assert.Equal(11,result[0].ReferencePlayers!.Count);Assert.Equal(11,first.Players.Count);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void IncompleteRosterOnlyGeneratesWithExplicitConsent(bool consent) {
        var club=Club(FootballGender.Female,3);var result=PlayableWorldPolicy.Prepare(club,199801,consent);
        if(consent){Assert.True(PlayableWorldPolicy.Complete(result.Players));Assert.Contains(result.Players,p=>p.PlayerOrigin==WorldPlayerOrigin.FICTIONAL_GENERATED);Assert.Equal(3,result.Players.Count(p=>p.PlayerOrigin==WorldPlayerOrigin.REAL_CURRENT_PROVIDER_REPORTED));}
        else{Assert.Equal(3,result.Players.Count);Assert.DoesNotContain(result.Players,p=>p.PlayerOrigin==WorldPlayerOrigin.FICTIONAL_GENERATED);}
    }
}
