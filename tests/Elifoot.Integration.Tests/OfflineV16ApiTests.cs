using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

public sealed class OfflineV16ApiTests
{
    [Fact]
    public async Task RequestFlagsSurviveStoredPreviewAndConfirmationIsFrozenAndIdempotent()
    {
        var folder=Directory.CreateTempSubdirectory("elifoot-v16-api-");
        try
        {
            async Task Save(string relative,object value)
            {
                string path=Path.Combine(folder.FullName,relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path,JsonSerializer.Serialize(value,OfflineCatalogLoader.Json));
            }
            var references=new List<string>();
            for(int i=0;i<128;i++)
            {
                string id=$"synthetic-{i:D3}";string relative=$"clubes/{id}.json";references.Add(relative);
                await Save($"identidades/{id}/identidade.json",new OfflineIdentityDefinition("1.1",id,id,id,"SYN",["#123456","#FFFFFF"],null));
                var players=Enumerable.Range(0,23).Select(p=>new OfflinePlayerV11($"{id}-p{p}",$"Synthetic athlete {i}/{p}",
                    p<3?"G":p<10?"D":p<17?"M":"A","BR",p%6,p==22)).ToList();
                await Save($"brasil/2026/{relative}",new OfflineClubV11("1.1",id,"BR",2026,20-i/8,null,i,null,
                    new($"coach-{id}","Synthetic coach","BR",new(["4-4-2"],[])),players,"READY"));
            }
            await Save("brasil/2026/manifest.json",new OfflineManifest("1.1","synthetic-2026",new("BR","Brasil","brasil"),2026,"Synthetic V16","READY",references));
            await using var app=new WebApplicationFactory<Program>().WithWebHostBuilder(b=> {
                b.UseSetting("Elifoot:AuthMode","LocalDev");b.UseSetting("Elifoot:DatabaseProvider","InMemory");
                b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");
                b.UseSetting("Catalog013A:Enabled","true");b.UseSetting("Catalog013A:DatabaseProvider","Sqlite");b.UseSetting("Catalog016B:Enabled","true");
                b.UseSetting("ConnectionStrings:Catalog013A",$"Data Source={Path.Combine(folder.FullName,"drafts.db")};Pooling=False");
                b.UseSetting("CatalogOffline:Root",folder.FullName);
            });
            using var client=app.CreateClient();
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",$"localdev-{Guid.NewGuid():N}");
            const string route="/api/world-creator/offline";
            var request=new OfflineWorldRequest("Synthetic V16",["synthetic-2026"],42,ApplyV16RosterPolicy:true,IncludeSurplusClub:true);
            var response=await client.PostAsJsonAsync(route+"/clubs",request);response.EnsureSuccessStatusCode();
            var clubs=(await response.Content.ReadFromJsonAsync<List<EligibleCatalogClub>>())!;
            request=request with {ClubKeys=clubs.Take(127).Select(c=>c.Key).ToList()};
            response=await client.PostAsJsonAsync(route+"/preview",request);response.EnsureSuccessStatusCode();
            var preview=(await response.Content.ReadFromJsonAsync<WorldCreatorPreview>())!;
            Assert.Empty(preview.Errors);Assert.Equal(128,preview.Clubs.Count);
            // Catalog changes after preview must not leak into the frozen confirmation.
            await File.WriteAllTextAsync(Path.Combine(folder.FullName,"brasil/2026/clubes/synthetic-000.json"),"{}");
            var first=await client.PostAsJsonAsync($"{route}/confirm/{preview.DraftId}",new {});first.EnsureSuccessStatusCode();
            var second=await client.PostAsJsonAsync($"{route}/confirm/{preview.DraftId}",new {});second.EnsureSuccessStatusCode();
            var a=await first.Content.ReadFromJsonAsync<JsonElement>();var b=await second.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(a.GetProperty("id").GetGuid(),b.GetProperty("id").GetGuid());
            var world=(await app.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(a.GetProperty("id").GetGuid()))!;
            Assert.Equal(preview.DataHash,world.OfflineCatalogOrigin!.DataHash);
            Assert.Equal(preview.Clubs.Sum(c=>c.Club.Players.Count),world.Players.Count);
            Assert.Equal(23,world.Players.Count(p=>p.ClubId==world.Clubs.Single(c=>c.OfflineIdentity!.CatalogClubId==OfflineSurplusV16.ClubId).Id));
        }
        finally
        {
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()),folder.FullName,StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("elifoot-v16-api-",folder.Name);folder.Delete(true);
        }
    }
}
