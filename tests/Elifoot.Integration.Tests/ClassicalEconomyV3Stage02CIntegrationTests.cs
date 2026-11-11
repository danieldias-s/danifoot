using System.Net;
using System.Net.Http.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class ClassicalEconomyV3Stage02CIntegrationTests
{
    private static WebApplicationFactory<Program> Factory(string file) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("Elifoot:AuthMode", "LocalDev");
            b.UseSetting("Elifoot:DatabaseProvider", "Sqlite");
            b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={file};Pooling=False");
            b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            b.UseSetting("Elifoot:DisableBackgroundClock", "true");
            b.ConfigureLogging(l => l.ClearProviders());
        });

    private static string DatabaseFile() =>
        Path.Combine(Path.GetTempPath(), $"elifoot-v3-02c-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ClassicalEconomyPreview_MatchesExecution_EnforcesFundsRevalidationConcurrencyAndIdempotency()
    {
        string file = DatabaseFile();
        var world = WorldSeedFactory.CreateSeededWorld(seed: 199802, totalClubs: 32);
        world.Auctions.Clear();
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(world, currencyFactorC: 1, recalculateInitialPlayerValues: true);

        var coaches = world.Clubs.Take(4)
            .Select(c => new HumanCoach { GameWorldId = world.Id, CurrentClubId = c.Id, Nickname = $"Coach-{c.ShortName}" })
            .ToArray();
        foreach (var coach in coaches)
        {
            world.HumanCoaches.Add(coach);
            world.Clubs.Single(c => c.Id == coach.CurrentClubId).HumanCoachId = coach.Id;
        }

        var sellerClub = world.Clubs[0]; sellerClub.Country = "Brasil";
        var buyerClub1 = world.Clubs[1]; buyerClub1.Country = "Brasil";
        var buyerClub2 = world.Clubs[2]; buyerClub2.Country = "Portugal";
        var poorBuyer = world.Clubs[3]; poorBuyer.Country = "Argentina"; poorBuyer.CashBalance = 50_000m;

        var target = world.Players.Last(p => p.ClubId == sellerClub.Id && p.Position == PlayerPosition.A);
        target.Strength = 40;
        target.Behavior = PlayerBehavior.FairPlay;
        target.PersonalModifierB = 2;
        target.PersonalModifierD = 2;
        target.IsStar = true;
        target.Nationality = "Brasil";
        target.SalaryPerRound = 3_000m;
        target.IsListedForTransfer = true;
        target.AskingPrice = 300_000m;

        await using (var factory = Factory(file))
        {
            using var client = factory.CreateClient();
            var repo = factory.Services.GetRequiredService<IWorldRepository>();
            await repo.SaveWorldAsync(world);

            // 1. Preview for Brazilian buyer (SameCountry) vs Portuguese buyer (EquivalentCountry) vs Argentine buyer (Foreigner)
            var previewBr = (await client.GetFromJsonAsync<TransferContractPreviewDto>(
                $"/api/worlds/{world.Id}/market/contract-preview?coachId={coaches[1].Id}&playerId={target.Id}"))!;
            var previewPt = (await client.GetFromJsonAsync<TransferContractPreviewDto>(
                $"/api/worlds/{world.Id}/market/contract-preview?coachId={coaches[2].Id}&playerId={target.Id}"))!;
            var previewAr = (await client.GetFromJsonAsync<TransferContractPreviewDto>(
                $"/api/worlds/{world.Id}/market/contract-preview?coachId={coaches[3].Id}&playerId={target.Id}"))!;

            Assert.Equal(4_300m, previewBr.SigningSalaryPerRound);
            Assert.Equal(4_300m, previewBr.CalculatedBaseSalary);
            Assert.Equal(185_440m, previewBr.MarketValue);
            Assert.Equal(344_704m, previewBr.CandidateSigningPrice);
            Assert.Equal("SAME_COUNTRY", previewBr.NationalityClass);
            Assert.Equal(1.65m, previewBr.AiHiringInterestFactor);
            Assert.Equal(2.0, previewBr.InflationIndex);
            Assert.Equal(20, previewBr.InflationDisplay);

            Assert.Equal(260_572m, previewPt.CandidateSigningPrice);
            Assert.Equal("EQUIVALENT_COUNTRY", previewPt.NationalityClass);

            Assert.Equal(185_440m, previewAr.CandidateSigningPrice);
            Assert.Equal("FOREIGNER", previewAr.NationalityClass);

            // 2. Insufficient funds rejection
            string buyPoorUrl = $"/api/worlds/{world.Id}/market/buy-listed?coachId={coaches[3].Id}&playerId={target.Id}&idempotencyKey=poor-buy&askingPrice=300000&salaryTermsToken={previewAr.SalaryTermsToken}";
            var poorResp = await client.PostAsync(buyPoorUrl, null);
            Assert.Equal(HttpStatusCode.BadRequest, poorResp.StatusCode);
            Assert.Equal(50_000m, poorBuyer.CashBalance);
            Assert.Equal(sellerClub.Id, target.ClubId);

            // 3. Stale terms rejection when inflation changes signing salary before confirmation
            world.Economy.InflationIndex = 2.5;
            await repo.SaveWorldAsync(world);
            string staleBuyUrl = $"/api/worlds/{world.Id}/market/buy-listed?coachId={coaches[1].Id}&playerId={target.Id}&idempotencyKey=stale-buy&askingPrice=300000&salaryTermsToken={previewBr.SalaryTermsToken}";
            var staleResp = await client.PostAsync(staleBuyUrl, null);
            Assert.Equal(HttpStatusCode.BadRequest, staleResp.StatusCode);
            Assert.Contains("CONTRACT_TERMS_CHANGED", await staleResp.Content.ReadAsStringAsync());

            // 4. Fresh preview at I = 2.5 and concurrent purchase by two buyers -> exactly 1 winner, no double charge, idempotent replay
            var freshPreview1 = (await client.GetFromJsonAsync<TransferContractPreviewDto>(
                $"/api/worlds/{world.Id}/market/contract-preview?coachId={coaches[1].Id}&playerId={target.Id}"))!;
            var freshPreview2 = (await client.GetFromJsonAsync<TransferContractPreviewDto>(
                $"/api/worlds/{world.Id}/market/contract-preview?coachId={coaches[2].Id}&playerId={target.Id}"))!;
            Assert.Equal(25, freshPreview1.InflationDisplay);

            decimal sellerBefore = sellerClub.CashBalance;
            string url1 = $"/api/worlds/{world.Id}/market/buy-listed?coachId={coaches[1].Id}&playerId={target.Id}&idempotencyKey=win-1&askingPrice=300000&salaryTermsToken={freshPreview1.SalaryTermsToken}";
            string url2 = $"/api/worlds/{world.Id}/market/buy-listed?coachId={coaches[2].Id}&playerId={target.Id}&idempotencyKey=win-2&askingPrice=300000&salaryTermsToken={freshPreview2.SalaryTermsToken}";

            var concurrentResults = await Task.WhenAll(client.PostAsync(url1, null), client.PostAsync(url2, null));
            Assert.Single(concurrentResults, r => r.IsSuccessStatusCode);
            int winIdx = Array.FindIndex(concurrentResults, r => r.IsSuccessStatusCode);
            string winUrl = winIdx == 0 ? url1 : url2;

            // Replay winning request with same idempotencyKey -> succeeds without double charge
            Assert.True((await client.PostAsync(winUrl, null)).IsSuccessStatusCode);
            Assert.Equal(sellerBefore + 300_000m, sellerClub.CashBalance);
            Assert.Equal(freshPreview1.SigningSalaryPerRound, target.SalaryPerRound);
            Assert.Equal(2, world.Transactions.Count(t => t.PlayerId == target.Id));
            Assert.Equal(0m, world.Transactions.Where(t => t.PlayerId == target.Id).Sum(t => t.Amount));
        }

        EfWorldRepository.ClearCacheForTesting();
        File.Delete(file);
    }
}
