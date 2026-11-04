using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Elifoot.Integration.Tests;

public class ProfileAppearance017ATests
{
    [Fact]
    public async Task TwoTrainersInOneWorldKeepIndependentVersionsWithoutChangingTheSaveAndRestoreAfterRestart()
    {
        var file = Path.Combine(Path.GetTempPath(), $"elifoot-017a-{Guid.NewGuid():N}.db");
        var connection = $"Data Source={file};Pooling=False";
        var options = new DbContextOptionsBuilder<ElifootDbContext>().UseSqlite(connection).Options;
        const string route = "/api/profile/appearance";
        Guid world;
        JsonElement before;
        string persistedSave;
        try
        {
            // Simulate an existing database that predates the preference table.
            await using (var legacy = new ElifootDbContext(options))
                await legacy.Database.EnsureCreatedAsync();
            await using (var server = Start(connection))
            using (var setup = server.CreateClient())
            using (var first = server.CreateClient())
            using (var second = server.CreateClient())
            {
                var a = await Login(first, "Treinador 1", "device-017a-one");
                var b = await Login(second, "Treinador 2", "device-017a-two");
                // A preference can be saved before joining a room or taking a club.
                Assert.Equal("CURRENT", await Mode(first));
                (await first.PutAsJsonAsync(route, new { mode = "CLASSIC_98" })).EnsureSuccessStatusCode();
                Assert.Equal("CURRENT", await Mode(second));
                var created = await setup.PostAsJsonAsync("/api/worlds", new CreateWorldRequest("Aparência 017A", MaxHumans: 2));
                created.EnsureSuccessStatusCode();
                world = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("gameWorldId").GetGuid();
                foreach (var (client, trainer, device) in new[] { (first, a, "device-017a-one"), (second, b, "device-017a-two") })
                    (await client.PostAsJsonAsync($"/api/worlds/{world}/join?coachId={trainer.CoachId}&nickname={Uri.EscapeDataString(trainer.Nickname)}&deviceId={device}", new { })).EnsureSuccessStatusCode();
                before = await setup.GetFromJsonAsync<JsonElement>($"/api/worlds/{world}/snapshot");
                Assert.Equal(2, before.GetProperty("readyCounter").GetProperty("totalHumans").GetInt32());
                await using (var db = new ElifootDbContext(options))
                    persistedSave = await db.Worlds.Where(w => w.Id == world).Select(w => w.SnapshotJson).SingleAsync();
                Assert.Equal(HttpStatusCode.Unauthorized, (await setup.GetAsync(route)).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await first.PutAsJsonAsync(route, new { mode = "unknown" })).StatusCode);
                foreach (var version in new[] { "CURRENT", "CLASSIC_98", "CURRENT", "CLASSIC_98" })
                    (await first.PutAsJsonAsync(route, new { mode = version })).EnsureSuccessStatusCode();
                Assert.Equal("CLASSIC_98", await Mode(first));
                Assert.Equal("CURRENT", await Mode(second));
                Assert.True(JsonElement.DeepEquals(before, await setup.GetFromJsonAsync<JsonElement>($"/api/worlds/{world}/snapshot")));
                await using var unchanged = new ElifootDbContext(options);
                Assert.Equal(persistedSave, await unchanged.Worlds.Where(w => w.Id == world).Select(w => w.SnapshotJson).SingleAsync());
            }
            await using var restarted = Start(connection);
            using var recovered = restarted.CreateClient();
            using var other = restarted.CreateClient();
            await Login(recovered, "TREINADOR 1", "device-017a-one");
            await Login(other, "Treinador 2", "device-017a-two");
            Assert.Equal("CLASSIC_98", await Mode(recovered));
            Assert.Equal("CURRENT", await Mode(other));
            Assert.Equal(before.GetProperty("roundVersion").GetInt64(),
                (await other.GetFromJsonAsync<JsonElement>($"/api/worlds/{world}/snapshot")).GetProperty("roundVersion").GetInt64());
        }
        finally { File.Delete(file); }
    }

    private static WebApplicationFactory<Program> Start(string connection) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Elifoot:DatabaseProvider", "Sqlite");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connection);
            builder.UseSetting("Elifoot:AuthMode", "LocalDev");
            builder.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            builder.UseSetting("Elifoot:DisableBackgroundClock", "true");
        });
    private static async Task<LocalAuthResponse> Login(HttpClient client, string nickname, string device)
    {
        var login = await client.PostAsJsonAsync("/api/auth/local-login", new LocalAuthRequest(nickname, device));
        login.EnsureSuccessStatusCode();
        var trainer = (await login.Content.ReadFromJsonAsync<LocalAuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", trainer.SessionToken);
        return trainer;
    }
    private static async Task<string?> Mode(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/profile/appearance")).GetProperty("mode").GetString();
}
