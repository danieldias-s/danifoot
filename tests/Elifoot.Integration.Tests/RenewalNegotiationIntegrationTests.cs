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
public class RenewalNegotiationIntegrationTests
{
    private static WebApplicationFactory<Program> Factory(string file) => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseEnvironment("Development"); b.UseSetting("Elifoot:AuthMode", "LocalDev");
        b.UseSetting("Elifoot:DatabaseProvider", "Sqlite");
        b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={file};Pooling=False");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false"); b.UseSetting("Elifoot:DisableBackgroundClock", "true");
        b.ConfigureLogging(l => l.ClearProviders());
    });

    private static (GameWorld W, HumanCoach H, Player P) Setup()
    {
        var w = WorldSeedFactory.CreateSeededWorld(totalClubs: 32); w.Auctions.Clear();
        var c = w.Clubs[0]; var h = new HumanCoach { GameWorldId = w.Id, CurrentClubId = c.Id };
        w.HumanCoaches.Add(h); c.HumanCoachId = h.Id;
        foreach (var p in w.Players.Where(p => p.ClubId == c.Id))
        { p.IsListedForTransfer = false; p.RenewedForSeason = null; p.ContractRoundsRemaining = 0; }
        var player = w.Players.Last(p => p.ClubId == c.Id && p.Position == PlayerPosition.A);
        player.SalaryPerRound = 100; player.Strength = 30;
        return (w, h, player);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task DecisionReceiptAndContractOrAuctionSurviveRestart(bool accept)
    {
        string file = Path.Combine(Path.GetTempPath(), $"elifoot-renewal-{Guid.NewGuid():N}.db");
        var (w, h, p) = Setup(); decimal offer = accept ? 150.50m : 100;
        string url = $"/api/worlds/{w.Id}/players/{p.Id}/renew-negotiation";
        RenewalNegotiationCommandDto command;
        try
        {
            await using (var factory = Factory(file))
            {
                using var client = factory.CreateClient();
                await factory.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
                var preview = (await client.GetFromJsonAsync<RenewalContractPreviewDto>(
                    $"/api/worlds/{w.Id}/players/{p.Id}/renew-preview?coachId={h.Id}&offeredSalary={offer.ToString(System.Globalization.CultureInfo.InvariantCulture)}"))!;
                Assert.Equal(accept, preview.OfferAccepted); Assert.Equal(offer, preview.OfferedSalary);
                command = new(h.Id, offer, accept, preview.TermsToken, "decision", preview.RoundVersion);
                var responses = await Task.WhenAll(client.PostAsJsonAsync(url, command), client.PostAsJsonAsync(url, command));
                Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode));
                Assert.Equal(accept ? 1 : 0, w.Economy.ContractHistory.Count);
                Assert.Equal(accept ? 0 : 1, w.Auctions.Count);
                Assert.False((await client.PostAsJsonAsync(url, command with { Accept = !accept })).IsSuccessStatusCode);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using (var restarted = Factory(file))
            {
                using var client = restarted.CreateClient();
                Assert.True((await client.PostAsJsonAsync(url, command)).IsSuccessStatusCode);
                var saved = (await restarted.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(w.Id))!;
                var restored = saved.Players.Single(x => x.Id == p.Id);
                Assert.Equal(accept ? offer : 100, restored.SalaryPerRound);
                Assert.Equal(accept ? h.CurrentClubId : null, restored.ClubId);
                Assert.Equal(accept, ClassicEconomyEngine.IsSeasonRenewed(saved, restored));
                Assert.Equal(accept ? 1 : 0, saved.Economy.ContractHistory.Count);
                Assert.Equal(accept ? 0 : 1, saved.Auctions.Count);
                if (!accept) { Assert.Equal(0, saved.Auctions.Single().MinimumBid); Assert.Equal(1, saved.Auctions.Single().DurationRounds); }
            }
        }
        finally { EfWorldRepository.ClearCacheForTesting(); File.Delete(file); }
    }

    [Fact] public async Task ApiRejectsOtherCoachReadyStaleAndInvalidOffersBeforeMutation()
    {
        string file = Path.Combine(Path.GetTempPath(), $"elifoot-renewal-guards-{Guid.NewGuid():N}.db");
        var (w, h, p) = Setup();
        try
        {
            await using var factory = Factory(file); using var client = factory.CreateClient();
            var repo = factory.Services.GetRequiredService<IWorldRepository>(); await repo.SaveWorldAsync(w);
            string previewUrl = $"/api/worlds/{w.Id}/players/{p.Id}/renew-preview?coachId={h.Id}";
            string url = $"/api/worlds/{w.Id}/players/{p.Id}/renew-negotiation";
            var preview = (await client.GetFromJsonAsync<RenewalContractPreviewDto>(previewUrl))!;
            var command = new RenewalNegotiationCommandDto(h.Id, 150, true, preview.TermsToken, "guard", preview.RoundVersion);
            foreach (var invalid in new[] {
                command with { CoachId = Guid.NewGuid() }, command with { OfferedSalary = -1 },
                command with { OfferedSalary = 100.001m }, command with { OfferedSalary = 100 },
                command with { TermsToken = "stale" }, command with { ExpectedRoundVersion = w.RoundVersion - 1 } })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, invalid)).StatusCode);
            h.IsReadyForRound = true; await repo.SaveWorldAsync(w);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, command)).StatusCode);
            Assert.Empty(w.Auctions); Assert.Empty(w.Economy.ContractHistory); Assert.Equal(100, p.SalaryPerRound);
        }
        finally { EfWorldRepository.ClearCacheForTesting(); File.Delete(file); }
    }
}
