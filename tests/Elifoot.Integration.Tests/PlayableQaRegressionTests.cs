using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Elifoot.Integration.Tests;

public class PlayableQaRegressionTests
{
    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider", "InMemory");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
        b.UseSetting("Elifoot:DisableBackgroundClock", "true");
    });

    [Fact]
    public async Task BrowserContract_UsesNamedEnums_AndSameSeedRoomsRemainIndependent()
    {
        await using var f = Factory(); using var c = f.CreateClient();
        var a = await c.PostAsJsonAsync("/api/worlds", new CreateWorldRequest("A"));
        var b = await c.PostAsJsonAsync("/api/worlds", new CreateWorldRequest("B"));
        var sa = (await a.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
        var sb = (await b.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
        Assert.NotEqual(sa.GameWorldId, sb.GameWorldId);
        Assert.Contains("\"roundState\":\"PREPARING\"", await a.Content.ReadAsStringAsync());
        var rooms = JsonDocument.Parse(await c.GetStringAsync("/api/worlds")).RootElement;
        Assert.Equal(2, rooms.GetArrayLength());
        Assert.NotEqual(rooms[0].GetProperty("inviteCode").GetString(), rooms[1].GetProperty("inviteCode").GetString());
    }

    [Fact]
    public async Task EditorSavesBehaviorStarClubAndManager_AndCalendarListsFullSeason()
    {
        await using var f = Factory(); using var c = f.CreateClient();
        var create = await c.PostAsJsonAsync("/api/worlds", new CreateWorldRequest("Editor QA"));
        var id = (await create.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!.GameWorldId;
        var pkg = (await c.GetFromJsonAsync<WorldPackageExportDto>($"/api/worlds/{id}/editor/export-json"))!;
        var player = pkg.Players[0] with { Strength = 50, Behavior = PlayerBehavior.FairPlay, IsStar = true };
        (await c.PostAsJsonAsync($"/api/worlds/{id}/editor/update-player", player)).EnsureSuccessStatusCode();
        var club = pkg.Clubs[0] with { Name = "Clube QA", PrimaryColorHex = "#123456" };
        (await c.PostAsJsonAsync($"/api/worlds/{id}/editor/update-club", club)).EnsureSuccessStatusCode();
        var manager = pkg.AiManagers[0] with { Name = "Técnico QA" };
        (await c.PostAsJsonAsync($"/api/worlds/{id}/editor/update-manager", manager)).EnsureSuccessStatusCode();
        var saved = (await c.GetFromJsonAsync<WorldPackageExportDto>($"/api/worlds/{id}/editor/export-json"))!;
        Assert.Equal(player, saved.Players.Single(p => p.Id == player.Id));
        Assert.Equal("Clube QA", saved.Clubs[0].Name);
        Assert.Equal("Técnico QA", saved.AiManagers[0].Name);
        var invalid = await c.PostAsJsonAsync($"/api/worlds/{id}/editor/update-player", player with { Strength = 51 });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        var calendar = JsonDocument.Parse(await c.GetStringAsync($"/api/worlds/{id}/calendar"));
        Assert.Equal(21, calendar.RootElement.GetArrayLength());
        Assert.Equal("League",calendar.RootElement[0].GetProperty("competition").GetString());
        Assert.Equal("CupFinal",calendar.RootElement[20].GetProperty("competition").GetString());
        (await c.PostAsJsonAsync($"/api/worlds/{id}/editor/create-club", club with { Name = "Novo Distrital" })).EnsureSuccessStatusCode();
        var expanded = (await c.GetFromJsonAsync<WorldPackageExportDto>($"/api/worlds/{id}/editor/export-json"))!;
        Assert.Equal(129, expanded.Clubs.Count);
        (await c.PostAsJsonAsync($"/api/worlds/{id}/editor/validate-package", expanded)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task LocalIdentity_IsStableAcrossServerInstances()
    {
        await using var f = Factory(); using var c = f.CreateClient();
        var request = new LocalAuthRequest("QA", "independent-device");
        var first = await c.PostAsJsonAsync("/api/auth/local-login", request);
        await using var g = Factory(); using var d = g.CreateClient();
        var second = await d.PostAsJsonAsync("/api/auth/local-login", request);
        Assert.Equal((await first.Content.ReadFromJsonAsync<LocalAuthResponse>())!.CoachId,
            (await second.Content.ReadFromJsonAsync<LocalAuthResponse>())!.CoachId);
    }
}
