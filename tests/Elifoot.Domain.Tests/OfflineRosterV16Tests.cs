using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public sealed class OfflineRosterV16Tests
{
    internal static EligibleCatalogClub Club(int number = 0, int count = 23, int goalkeepers = 3)
    {
        string id = $"synthetic-{number}";
        var players = Enumerable.Range(0,count).Select(i => new OfflinePlayer($"{id}-p{i}", $"Synthetic {i}",
            i < goalkeepers ? "G" : new[] {"D","M","A"}[(i-goalkeepers)%3], null,"BR",i%6,
            i == count-1 && (i-goalkeepers)%3 != 0)).ToList();
        var definition = new OfflineClub("1.1",id,"BR",2026,id,id,"SYN",["#123456","#FFFFFF"],null,
            new($"coach-{number}","Synthetic coach","BR",new(["4-4-2"],[])),players,"READY");
        var origin = new OfflineClubSource("synthetic","BR","Brasil",2026,$"hash-{number}",definition,V11:new(
            new("1.1",id,id,id,"SYN",["#123456","#FFFFFF"],null),20-number/8,null,number));
        var club = OfflineWorldFactory.Eligible(origin,FootballGender.Male);
        return club with {Players=players.Select((p,i)=>new CatalogAthlete(p.Id!,id,"offline-v1.1",FootballGender.Male,"2026",p.Name!,p.Position!,1+i%50,"Synthetic fixture",DateTime.UnixEpoch,CanonicalPlayerId:$"offline:BR:2026:{id}:{p.Id}")).ToList()};
    }

    [Theory]
    [InlineData(1,20,21)] [InlineData(2,19,20)] [InlineData(3,19,20)] [InlineData(4,17,19)] [InlineData(5,13,23)]
    public void BoundsGoalkeepersSourceAndDeterminism(int division,int minimum,int maximum)
    {
        var input=Club();string before=JsonSerializer.Serialize(input);
        var counts=new HashSet<int>();var fingerprints=new HashSet<string>();
        for(int seed=0;seed<100;seed++)
        {
            var selected=OfflineRosterV16.Select(new(division,input),seed).Club;
            counts.Add(selected.Players.Count); fingerprints.Add(string.Join(",",selected.Players.Select(p=>p.PlayerExternalId)));
            Assert.InRange(selected.Players.Count,minimum,maximum);
            Assert.Equal(division<5?2:selected.Players.Count==23?3:2,selected.Players.Count(p=>p.Position=="G"));
            Assert.Equal(23,selected.ReferencePlayers!.Count);
            Assert.All(selected.Players,p=>Assert.Contains(p,input.Players));
            Assert.Equal(JsonSerializer.Serialize(selected),JsonSerializer.Serialize(OfflineRosterV16.Select(new(division,input),seed).Club));
        }
        Assert.Contains(minimum,counts);Assert.Contains(maximum,counts);Assert.True(fingerprints.Count>1);
        Assert.Equal(before,JsonSerializer.Serialize(input));
    }
    [Theory]
    [InlineData(11,1,5,false)] [InlineData(13,1,5,true)] [InlineData(17,2,4,true)]
    [InlineData(19,2,3,true)] [InlineData(20,2,1,true)] [InlineData(21,2,1,true)]
    [InlineData(23,3,1,true)] [InlineData(23,1,1,false)] [InlineData(19,2,1,false)]
    public void InsufficientCandidatesFailWithoutCompletion(int count,int keepers,int division,bool valid)
    {
        var input=new WorldPreviewClub(division,Club(count:count,goalkeepers:keepers));
        if(valid) Assert.InRange(OfflineRosterV16.Select(input,72).Club.Players.Count,13,23);
        else Assert.Throws<ArgumentException>(()=>OfflineRosterV16.Select(input,72));
    }
    [Fact]
    public void StarAndDocumentedProdigyAndStarterAreRetainedWhenPositionsPermit()
    {
        var input=Club();var star=input.OfflineSource!.Definition.Players!.Single(p=>p.IsStar==true);
        var research=new List<OfflineRetentionEvidence> {new(input.Key,input.Players[4].PlayerExternalId,"Synthetic research",IsProdigy:true),new(input.Key,input.Players[5].PlayerExternalId,"Synthetic research",ImportantStarter:true,Minutes:2000)};
        var selected=OfflineRosterV16.Select(new(4,input),42,research).Club;
        Assert.Contains(selected.Players,p=>p.PlayerExternalId==star.Id);
        Assert.All(research,r=>Assert.Contains(selected.Players,p=>p.PlayerExternalId==r.PlayerId));
        Assert.Throws<ArgumentException>(()=>OfflineRosterV16.Select(new(4,input),42,[research[0] with {Evidence=""}]));
    }
    [Fact]
    public void NewRequestOptionsDoNotChangeLegacySerialization()
    {
        var request=new OfflineWorldRequest("Synthetic",["synthetic"],42);
        Assert.Equal(JsonSerializer.Serialize(new {request.Name,request.CatalogIds,request.Seed,request.MaxHumans,request.ClubKeys,request.Assignments}),JsonSerializer.Serialize(request));
    }
    [Fact]
    public void DifferentInitialDivisionSelectsAgainOnlyForNewPreview()
    {
        var clubs=Enumerable.Range(0,128).Select(i=>Club(i)).ToList();
        var request=new OfflineWorldRequest("Synthetic",["synthetic"],72,ApplyV16RosterPolicy:true);
        var first=OfflineWorldFactory.Preview(request,clubs);
        var next=OfflineWorldFactory.Preview(request with {Assignments=[new(clubs[0].Key,4),new(clubs[24].Key,1)]},clubs);
        Assert.Empty(next.Errors);Assert.NotEqual(first.DataHash,next.DataHash);
        Assert.InRange(next.Clubs.Single(c=>c.Club.Key==clubs[0].Key).Club.Players.Count,17,19);
        Assert.InRange(first.Clubs.Single(c=>c.Club.Key==clubs[0].Key).Club.Players.Count,20,21);
    }
    [Fact]
    public void PreviewCreateFreezeSelectedAndExcludedAndSaveRestoresWithoutRecalculation()
    {
        var clubs=Enumerable.Range(0,128).Select(i=>Club(i)).ToList();
        var request=new OfflineWorldRequest("Synthetic V16",["synthetic"],42,ApplyV16RosterPolicy:true);
        var preview=OfflineWorldFactory.Preview(request,clubs);
        Assert.Empty(preview.Errors);
        Assert.Equal(preview.DataHash,OfflineWorldFactory.Preview(request,clubs.AsEnumerable().Reverse().ToList()).DataHash);
        var world=OfflineWorldFactory.Create(request,preview);
        Assert.Equal(preview.Clubs.Sum(c=>c.Club.Players.Count),world.Players.Count);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        Assert.Equal(world.OfflineCatalogOrigin!.DataHash,restored.OfflineCatalogOrigin!.DataHash);
        var retained=restored.Players.Select(p=>(p.Id,p.Strength,p.Behavior,p.OfflineIdentity)).ToList();
        restored.Clubs[0].DivisionTier=(DivisionTier)4;
        Assert.Equal(retained,restored.Players.Select(p=>(p.Id,p.Strength,p.Behavior,p.OfflineIdentity)).ToList());
        Assert.Equal(23,restored.OfflineCatalogOrigin.Clubs[0].Club.ReferencePlayers!.Count);
        Assert.NotEqual(preview.DataHash,OfflineWorldFactory.Preview(request with {Seed=43},clubs).DataHash);
    }
}
