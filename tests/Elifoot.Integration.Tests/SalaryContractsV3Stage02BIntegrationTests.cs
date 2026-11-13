using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class SalaryContractsV3Stage02BIntegrationTests
{
    private static WebApplicationFactory<Program> Factory(string file)=>new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{
        b.UseEnvironment("Development");b.UseSetting("Elifoot:AuthMode","LocalDev");b.UseSetting("Elifoot:DatabaseProvider","Sqlite");
        b.UseSetting("ConnectionStrings:DefaultConnection",$"Data Source={file};Pooling=False");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");
        b.ConfigureLogging(l=>l.ClearProviders());
    });
    private static (GameWorld W,HumanCoach[] H,Player P,SalaryRaiseRequest R) Setup()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:32);w.Auctions.Clear();
        foreach(var c in w.Clubs)c.HumanCoachId=Guid.NewGuid();
        var h=w.Clubs.Take(3).Select(c=>new HumanCoach{GameWorldId=w.Id,CurrentClubId=c.Id}).ToArray();
        foreach(var coach in h){w.HumanCoaches.Add(coach);w.Clubs.Single(c=>c.Id==coach.CurrentClubId).HumanCoachId=coach.Id;}
        var p=w.Players.Last(p=>p.ClubId==h[0].CurrentClubId&&p.Position==PlayerPosition.A);
        p.IsListedForTransfer=false;p.SalaryPerRound=100;p.ContractRoundsRemaining=0;p.Strength=30;
        var r=new SalaryRaiseRequest{Id=Guid.NewGuid(),WorldId=w.Id,ClubId=p.ClubId!.Value,PlayerId=p.Id,
            Season=1,Round=1,CurrentSalary=100,RequestedSalary=110,RenewalMatches=20};
        w.Economy.SalaryRequests.Add(r);return(w,h,p,r);
    }
    private static string DatabaseFile()=>Path.Combine(Path.GetTempPath(),$"elifoot-v3-02b-{Guid.NewGuid():N}.db");

    [Fact] public async Task PreviewRevalidationConcurrentPurchaseAndReceiptSurviveSqliteRestart()
    {
        string file=DatabaseFile();var(w,h,p,r)=Setup();w.Economy.SalaryRequests.Clear();p.IsListedForTransfer=true;p.AskingPrice=100_000;
        string winningUrl="";Guid winner=Guid.Empty;decimal expectedSalary=0;
        await using(var factory=Factory(file))
        {
            using var client=factory.CreateClient();var repo=factory.Services.GetRequiredService<IWorldRepository>();await repo.SaveWorldAsync(w);
            string previewUrl=$"/api/worlds/{w.Id}/market/contract-preview?coachId={h[1].Id}&playerId={p.Id}";
            var old=(await client.GetFromJsonAsync<TransferContractPreviewDto>(previewUrl))!;
            string Buy(int index,string? token,string key)=>$"/api/worlds/{w.Id}/market/buy-listed?coachId={h[index].Id}&playerId={p.Id}&idempotencyKey={key}&askingPrice=100000"+(token==null?"":$"&salaryTermsToken={token}");
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync(Buy(1,null,"missing"),null)).StatusCode);
            p.Strength=50;await repo.SaveWorldAsync(w);
            var stale=await client.PostAsync(Buy(1,old.SalaryTermsToken,"stale"),null);
            Assert.Contains("CONTRACT_TERMS_CHANGED",await stale.Content.ReadAsStringAsync());Assert.Equal(h[0].CurrentClubId,p.ClubId);
            var current=(await client.GetFromJsonAsync<TransferContractPreviewDto>(previewUrl))!;expectedSalary=current.SigningSalaryPerRound;
            var urls=new[]{Buy(1,current.SalaryTermsToken,"buyer-1"),Buy(2,current.SalaryTermsToken,"buyer-2")};
            var results=await Task.WhenAll(urls.Select(u=>client.PostAsync(u,null)));Assert.Single(results,x=>x.IsSuccessStatusCode);
            int won=Array.FindIndex(results,x=>x.IsSuccessStatusCode);winner=h[won+1].CurrentClubId!.Value;winningUrl=urls[won];
            Assert.Equal(winner,p.ClubId);Assert.Equal(expectedSalary,p.SalaryPerRound);
            Assert.Equal(2,w.Transactions.Count(t=>t.PlayerId==p.Id));Assert.Equal(0,w.Transactions.Where(t=>t.PlayerId==p.Id).Sum(t=>t.Amount));
            Assert.True((await client.PostAsync(winningUrl,null)).IsSuccessStatusCode);
        }
        EfWorldRepository.ClearCacheForTesting();
        await using(var restart=Factory(file))
        {
            using var client=restart.CreateClient();Assert.True((await client.PostAsync(winningUrl,null)).IsSuccessStatusCode);
            var restored=(await restart.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(w.Id))!;
            Assert.Equal(winner,restored.Players.Single(x=>x.Id==p.Id).ClubId);Assert.Equal(expectedSalary,restored.Players.Single(x=>x.Id==p.Id).SalaryPerRound);
            Assert.Equal(2,restored.Transactions.Count(t=>t.PlayerId==p.Id));
        }
        EfWorldRepository.ClearCacheForTesting();File.Delete(file);
    }

    [Fact] public async Task AcceptedRaiseBlocksRenewalUntilNextSeason()
    {
        string file=DatabaseFile();var(w,h,p,r)=Setup();w.RoundState=RoundState.RESULTS;w.RoundNumber=w.TotalRoundsInSeason;
        await using(var factory=Factory(file))
        {
            using var client=factory.CreateClient();await factory.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
            string next=$"/api/worlds/{w.Id}/next-round?expectedSeasonNumber=1&expectedRoundNumber={w.RoundNumber}";
            var blocked=await client.PostAsync(next,null);Assert.Equal(HttpStatusCode.Conflict,blocked.StatusCode);
            Assert.Contains("SALARY_PENDING_TRANSITION",await blocked.Content.ReadAsStringAsync());Assert.Equal(1,w.SeasonNumber);Assert.Empty(w.SeasonHistories);
            var decision=new SalaryDecisionCommandDto(h[0].Id,true,"accept");
            string salary=$"/api/worlds/{w.Id}/clubs/{r.ClubId}/salary-requests/{r.Id}/respond";
            Assert.True((await client.PostAsJsonAsync(salary,decision)).IsSuccessStatusCode);
            Assert.True((await client.PostAsJsonAsync(salary,decision)).IsSuccessStatusCode);Assert.Equal(1,r.DecisionVersion);
            Assert.Equal(110,p.SalaryPerRound);Assert.False(ClassicEconomyEngine.IsSeasonRenewed(w,p));
            string clubDetail = $"/api/worlds/{w.Id}/clubs/{r.ClubId}";
            var adjusted = (await client.GetFromJsonAsync<ClubDetailDto>(clubDetail))!.Squad.Single(x => x.Id == p.Id);
            Assert.True(adjusted.SalaryRaiseAcceptedForSeason);
            Assert.False(adjusted.ContractRenewedForSeason);
            string playerPath = $"/api/worlds/{w.Id}/players/{p.Id}";
            string preview = $"{playerPath}/renew-preview?coachId={h[0].Id}";
            string renew = $"{playerPath}/renew?coachId={h[0].Id}&additionalRounds=0";
            foreach (var response in new[]
            {
                await client.GetAsync(preview),
                await client.GetAsync(preview + "&offeredSalary=110"),
                await client.PostAsync(renew + "&idempotencyKey=blocked", null),
                await client.PostAsJsonAsync(playerPath + "/renew-negotiation",
                    new RenewalNegotiationCommandDto(h[0].Id, 110, true, "old-terms", "blocked-offer"))
            })
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("aumento salarial aceito", await response.Content.ReadAsStringAsync());
            }
            Assert.Empty(w.Transactions.Where(t => t.PlayerId == p.Id && t.Category == FinancialTransactionCategory.ContractRenewal));
            Assert.Single(w.Economy.ContractHistory, record => record.PlayerId == p.Id);
            Assert.True((await client.PostAsync(next,null)).IsSuccessStatusCode);Assert.True((await client.PostAsync(next,null)).IsSuccessStatusCode);
            Assert.Equal(2,w.SeasonNumber);Assert.Single(w.SeasonHistories);
            var nextSeason = (await client.GetFromJsonAsync<ClubDetailDto>(clubDetail))!.Squad.Single(x => x.Id == p.Id);
            Assert.False(nextSeason.SalaryRaiseAcceptedForSeason);
            Assert.False(nextSeason.ContractRenewedForSeason);
            var renewal = await client.GetFromJsonAsync<RenewalContractPreviewDto>(preview);
            Assert.True((await client.PostAsync(renew + $"&idempotencyKey=next-season&renewalTermsToken={renewal!.TermsToken}", null)).IsSuccessStatusCode);
            Assert.Equal(renewal.RequiredSalary,p.SalaryPerRound);Assert.Equal(h[0].CurrentClubId,p.ClubId);
        }
        EfWorldRepository.ClearCacheForTesting();File.Delete(file);
    }

    [Fact] public async Task SqliteFailureRollsBackRenewalFeeAndSameReceiptRetriesOneContract()
    {
        string file=DatabaseFile();var(w,h,p,r)=Setup();decimal cash=w.Clubs[0].CashBalance;
        var other=w.Players.First(x=>x.ClubId==p.ClubId&&x.Id!=p.Id);
        r.PlayerId=other.Id;r.CurrentSalary=other.SalaryPerRound;r.RequestedSalary=other.SalaryPerRound+10;
        await using(var factory=Factory(file))
        {
            using var client=factory.CreateClient();await factory.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
            using(var scope=factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.ExecuteSqlRawAsync("""
                    CREATE TRIGGER stage02b_fail_fee BEFORE INSERT ON ef_financial_ledger
                    WHEN CAST(NEW."Amount" AS REAL) < 0
                    BEGIN SELECT RAISE(ABORT, 'STAGE02B_CONTROLLED_FEE_FAILURE'); END;
                    """);
            var preview=await client.GetFromJsonAsync<RenewalContractPreviewDto>($"/api/worlds/{w.Id}/players/{p.Id}/renew-preview?coachId={h[0].Id}");
            string url=$"/api/worlds/{w.Id}/players/{p.Id}/renew?coachId={h[0].Id}&additionalRounds=20&idempotencyKey=renew&renewalTermsToken={preview!.TermsToken}";
            var failure=await client.PostAsync(url,null);Assert.Equal(HttpStatusCode.InternalServerError,failure.StatusCode);
            Assert.Contains("CONTRACT_PERSISTENCE_FAILED",await failure.Content.ReadAsStringAsync());
            using(var scope=factory.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
                var json=await db.Worlds.AsNoTracking().Where(x=>x.Id==w.Id).Select(x=>x.SnapshotJson).SingleAsync();
                var durable=JsonSerializer.Deserialize<GameWorld>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
                Assert.Equal(cash,durable.Clubs[0].CashBalance);Assert.Null(durable.Players.Single(x=>x.Id==p.Id).RenewedForSeason);
                Assert.Equal(SalaryRequestStatus.PENDING,durable.Economy.SalaryRequests.Single().Status);
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER stage02b_fail_fee");
            }
            Assert.True((await client.PostAsync(url,null)).IsSuccessStatusCode);Assert.True((await client.PostAsync(url,null)).IsSuccessStatusCode);
            Assert.Single(w.Transactions,t=>t.Category==FinancialTransactionCategory.ContractRenewal);
            Assert.Single(w.Economy.ContractHistory);Assert.Equal(cash-ClassicEconomyEngine.SeasonRenewalFee(w,p),w.Clubs[0].CashBalance);
            Assert.Equal(0,r.DecisionVersion);Assert.Equal(SalaryRequestStatus.PENDING,r.Status);
        }
        EfWorldRepository.ClearCacheForTesting();File.Delete(file);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task LegacyPendingRequestWaitsForHumanThenRecoversAndPersists(bool accept)
    {
        string file=DatabaseFile();var(w,h,p,r)=Setup();w.SeasonNumber=2;w.RoundNumber=w.TotalRoundsInSeason;w.RoundState=RoundState.RESULTS;
        await using(var factory=Factory(file))
        {
            using var client=factory.CreateClient();await factory.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
            var result=await client.PostAsync($"/api/worlds/{w.Id}/next-round",null);
            Assert.Equal(HttpStatusCode.Conflict,result.StatusCode);Assert.Contains("SALARY_PENDING_TRANSITION",await result.Content.ReadAsStringAsync());
            Assert.Equal(100,p.SalaryPerRound);Assert.Equal(h[0].CurrentClubId,p.ClubId);Assert.Empty(w.Auctions);Assert.Equal(2,w.SeasonNumber);
            string responseUrl=$"/api/worlds/{w.Id}/clubs/{r.ClubId}/salary-requests/{r.Id}/respond";
            var command=new SalaryDecisionCommandDto(h[0].Id,accept,"old");
            Assert.True((await client.PostAsJsonAsync(responseUrl,command)).IsSuccessStatusCode);
            Assert.True((await client.PostAsJsonAsync(responseUrl,command)).IsSuccessStatusCode);
            Assert.Equal(accept?SalaryRequestStatus.ACCEPTED:SalaryRequestStatus.REFUSED,r.Status);
            Assert.Equal(1,r.Season);Assert.Equal(1,r.DecisionVersion);Assert.Equal(accept?110:100,p.SalaryPerRound);
            Assert.Equal(accept?0:1,w.Auctions.Count);
            if(!accept){Assert.Equal(1,w.Auctions.Single().DurationRounds);Assert.Equal(0,w.Auctions.Single().MinimumBid);}
            Assert.True((await client.PostAsync($"/api/worlds/{w.Id}/next-round",null)).IsSuccessStatusCode);
            Assert.Equal(3,w.SeasonNumber);
            EfWorldRepository.ClearCacheForTesting();
            var restored=(await factory.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(w.Id))!;
            Assert.Equal(r.Status,restored.Economy.SalaryRequests.Single().Status);
            Assert.Equal(accept?h[0].CurrentClubId:null,restored.Players.Single(x=>x.Id==p.Id).ClubId);
        }
        EfWorldRepository.ClearCacheForTesting();File.Delete(file);
    }
}
