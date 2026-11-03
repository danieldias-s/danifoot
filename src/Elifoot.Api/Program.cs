using System.Text;
using Elifoot.Api;
using Elifoot.Api.Hubs;
using Elifoot.Api.Services;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

string authMode = builder.Configuration["Elifoot:AuthMode"] ?? "LocalDev";
if (!string.Equals(authMode, "LocalDev", StringComparison.OrdinalIgnoreCase) &&
    string.IsNullOrWhiteSpace(builder.Configuration["Elifoot:ProductionJwtSecret"]))
{
    throw new InvalidOperationException(
        "SECURITY BLOCK: O modo de execução não é 'LocalDev', mas 'Elifoot:ProductionJwtSecret' não foi configurado. " +
        "O login simplificado por Nickname/DeviceId é exclusivo para DEV local e não pode ser exposto na internet.");
}

string dbProvider = builder.Configuration["Elifoot:DatabaseProvider"] ?? "PostgreSQL";
string connString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=127.0.0.1;Port=5432;Database=elifoot_classic;Username=postgres;Password=123456";

if (string.Equals(dbProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IWorldRepository, InMemoryWorldRepository>();
}
else
{
    builder.Services.AddDbContext<ElifootDbContext>(options =>
    {
        if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlite(connString);
        }
        else
        {
            options.UseNpgsql(connString);
        }
    });
    builder.Services.AddSingleton<IWorldRepository, EfWorldRepository>();
}

bool worldCreator013A = builder.AddWorldCreator013A();
builder.AddImport016A();
builder.AddWorldCatalog016B();
builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomConnectionRegistry>();
builder.Services.AddSingleton<IMatchRoomNotificationPublisher, SignalRMatchRoomNotificationPublisher>();
builder.Services.AddScoped<MultiplayerRoundOrchestrator>();
builder.Services.AddScoped<ClubAndMarketApplicationService>();
builder.Services.AddScoped<PlayerRankingsApplicationService>();
builder.Services.AddHostedService<RoomClockBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();
var roomConnections = app.Services.GetRequiredService<RoomConnectionRegistry>();
async Task<LiveRoundSnapshotDto> RoomSnapshot(GameWorld world, CancellationToken ct)
{
    var sem = MultiplayerRoundOrchestrator.GetRoomLock(world.Id);
    await sem.WaitAsync(ct);
    try { return roomConnections.WithPresence(MultiplayerRoundOrchestrator.BuildLiveRoundSnapshotDto(world)); }
    finally { sem.Release(); }
}

app.UseCors();

// Initialize database schema and seed default 128-club world if configured
using (var scope = app.Services.CreateScope())
{
    if (!string.Equals(dbProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
    {
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    bool autoSeed = builder.Configuration.GetValue<bool>("Elifoot:AutoSeedDefaultWorld", true);
    if (autoSeed)
    {
        var repo = scope.ServiceProvider.GetRequiredService<IWorldRepository>();
        var existing = await repo.ListWorldsAsync();
        if (existing.Count == 0)
        {
            var orchestrator = scope.ServiceProvider.GetRequiredService<MultiplayerRoundOrchestrator>();
            await orchestrator.CreateWorldAsync(new CreateWorldRequest(
                WorldName: "Campeonato Brasileiro 98 (128 Clubes)",
                MaxHumans: 8,
                TotalClubs: 128,
                Seed: 199801,
                TickIntervalMs: 220,
                PauseTimeoutSeconds: 60,
                AllowClassic505: true,
                InviteCode: "ELIF98"));
        }
    }
}

app.MapHub<GameRoomHub>("/hubs/gameroom");
app.MapPlayerRankings();
if (worldCreator013A) { await app.InitializeWorldCreator013A(); app.MapWorldCreator013A(); }
if (builder.Configuration.GetValue<bool>("Catalog016A:Enabled")) {await app.InitializeImport016A();app.MapImport016A();}
if (builder.Configuration.GetValue<bool>("Catalog016B:Enabled")) {await app.InitializeWorldCatalog016B();app.MapWorldCatalog016B();}

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "OK",
    authMode,
    dbProvider,
    timestampUtc = DateTime.UtcNow
}));

// 1. LocalDev Auth
app.MapPost("/api/auth/local-login", (LocalAuthRequest req) =>
{
    string nick = string.IsNullOrWhiteSpace(req.Nickname) ? "Técnico" : req.Nickname.Trim();
    string devId = string.IsNullOrWhiteSpace(req.DeviceId) ? Guid.NewGuid().ToString("N") : req.DeviceId.Trim();
    // Deterministic CoachId from Nickname + DeviceId so reconnecting from the same device restores identity!
    byte[] identityHash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes($"{nick.ToUpperInvariant()}\n{devId}"));
    Guid coachId = new Guid(identityHash.AsSpan(0, 16));

    return Results.Ok(new LocalAuthResponse(
        CoachId: coachId,
        Nickname: nick,
        DeviceId: devId,
        SessionToken: $"localdev-{coachId:N}",
        AuthModeWarning: "MODO LOCAL_DEV: Autenticação simplificada apenas para desenvolvimento local. Não expor diretamente na internet."));
});

// 2. Worlds / Rooms
app.MapGet("/api/worlds", async (IWorldRepository repo, Guid? coachId, string? deviceId, CancellationToken ct) =>
{
    var worlds = await repo.ListWorldsAsync(ct);
    var summaries = new List<object>();
    foreach (var w in worlds)
    {
        var sem = MultiplayerRoundOrchestrator.GetRoomLock(w.Id);
        await sem.WaitAsync(ct);
        try
        {
            var counter = roomConnections.WithPresence(MultiplayerRoundOrchestrator.BuildReadyCounterDto(w));
            var own = w.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
            var device = deviceId?.Trim();
            if (string.Equals(authMode, "LocalDev", StringComparison.OrdinalIgnoreCase) && own == null && !string.IsNullOrWhiteSpace(device) && device != "dev") {
                var candidates = w.HumanCoaches.Where(h => string.Equals((h.DeviceId ?? string.Empty).Trim(), device, StringComparison.Ordinal)).ToList();
                if (candidates.Count == 1) own = candidates[0];
            }
            summaries.Add(new {
                ReconnectCoachId = own?.Id,
                w.Id, w.Name, w.InviteCode, w.Seed, w.TotalClubs, w.MaxHumans,
                CurrentHumans = w.HumanCoaches.Count,
                ConnectedHumans = counter.Participants.Count(p => p.IsConnected),
                AvailableSeats = Math.Max(0, w.MaxHumans - w.HumanCoaches.Count),
                w.SeasonNumber, w.RoundNumber, RoundState = w.RoundState.ToString(), ReadyCounter = counter,
                w.GameModeId, HasInitialLobby=w.InitialLobby!=null, LobbyStarted=w.InitialLobby?.StartedAt!=null
            });
        }
        finally { sem.Release(); }
    }
    return Results.Ok(summaries);
});

app.MapPost("/api/worlds", async (CreateWorldRequest req, MultiplayerRoundOrchestrator orchestrator, CancellationToken ct) =>
{
    var world = await orchestrator.CreateWorldAsync(req, ct);
    return Results.Ok(await RoomSnapshot(world, ct));
});

app.MapPost("/api/worlds/{worldId:guid}/join", async (
    Guid worldId,
    Guid coachId,
    string nickname,
    string? deviceId,
    Guid? preferredClubId,
    bool? joinUnemployed,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var (ok, err, coach, world) = await orchestrator.JoinWorldAndAssignClubAsync(
        worldId, coachId, nickname, deviceId ?? "dev", preferredClubId, joinUnemployed ?? false, ct, allowDeviceReconnect: string.Equals(authMode, "LocalDev", StringComparison.OrdinalIgnoreCase));
    if (!ok || world == null) return Results.BadRequest(new { error = err, code = err?.StartsWith("Sala lotada:", StringComparison.Ordinal) == true ? "ROOM_FULL" : "JOIN_REJECTED" });

    return Results.Ok(new
    {
        coachId = coach!.Id,
        nickname = coach.Nickname,
        clubId = coach.CurrentClubId,
        isUnemployed = coach.IsUnemployed,
        isHost = coach.IsHost,
        snapshot = await RoomSnapshot(world, ct)
    });
});

app.MapPost("/api/worlds/join-by-code", async (
    string inviteCode,
    Guid coachId,
    string nickname,
    string? deviceId,
    Guid? preferredClubId,
    IWorldRepository repo,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var world = await repo.GetWorldByInviteCodeAsync(inviteCode, ct);
    if (world == null) return Results.NotFound(new { error = $"Sala com código '{inviteCode}' não encontrada." });

    var (ok, err, coach, updatedWorld) = await orchestrator.JoinWorldAndAssignClubAsync(
        world.Id, coachId, nickname, deviceId ?? "dev", preferredClubId, false, ct, allowDeviceReconnect: string.Equals(authMode, "LocalDev", StringComparison.OrdinalIgnoreCase));
    if (!ok || updatedWorld == null) return Results.BadRequest(new { error = err, code = err?.StartsWith("Sala lotada:", StringComparison.Ordinal) == true ? "ROOM_FULL" : "JOIN_REJECTED" });

    return Results.Ok(new
    {
        worldId = updatedWorld.Id,
        coachId = coach!.Id,
        nickname = coach.Nickname,
        clubId = coach.CurrentClubId,
        isUnemployed = coach.IsUnemployed,
        snapshot = await RoomSnapshot(updatedWorld, ct)
    });
});

app.MapGet("/api/worlds/{worldId:guid}/snapshot", async (Guid worldId, IWorldRepository repo, CancellationToken ct) =>
{
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();
    return Results.Ok(await RoomSnapshot(world, ct));
});

app.MapGet("/api/worlds/{worldId:guid}/calendar", async (Guid worldId, IWorldRepository repo, CancellationToken ct) =>
{
    var w = await repo.GetWorldAsync(worldId, ct);
    if (w == null) return Results.NotFound();
    var clubs = w.Clubs.ToDictionary(c => c.Id);
    var schedule = CompetitionAndSeasonEngine.BuildSeasonRoundSchedule(w);
    return Results.Ok(schedule.Select((type, i) => new {
        roundNumber = i + 1, competition = type.ToString(),
        fixtures = w.Fixtures.Where(f => f.SeasonNumber == w.SeasonNumber && f.RoundNumber == i + 1)
            .Select(f => new { f.Id, home = clubs[f.HomeClubId].Name, away = clubs[f.AwayClubId].Name,
                f.HomeGoals, f.AwayGoals, f.IsFinished, f.HomeClubId, f.AwayClubId })
    }));
});

app.MapPost("/api/worlds/{worldId:guid}/editor/update-club", async (
    Guid worldId, ClubExportDto edited, IWorldRepository repo, CancellationToken ct) =>
{
    var w = await repo.GetWorldAsync(worldId, ct);
    if (w == null) return Results.NotFound();
    if (w.CatalogOrigin != null || w.CustomAssemblyOrigin != null || w.HumanCoaches.Count > 0 || w.SeasonNumber > 1 || w.RoundNumber > 1)
        return Results.Conflict(new { error = "Edite uma montagem em CRIAR NOVO CAMPEONATO. Saves confirmados não permitem edição direta." });
    if (w.RoundState != RoundState.PREPARING || w.HumanCoaches.Any(h => h.IsReadyForRound))
        return Results.BadRequest(new { error = "Retire pronto de todos os técnicos antes de editar." });
    if (string.IsNullOrWhiteSpace(edited.Name) || !System.Text.RegularExpressions.Regex.IsMatch(edited.PrimaryColorHex, "^#[0-9a-fA-F]{6}$") ||
        !System.Text.RegularExpressions.Regex.IsMatch(edited.SecondaryColorHex, "^#[0-9a-fA-F]{6}$"))
        return Results.BadRequest(new { error = "Informe nome e cores no formato #RRGGBB." });
    var club = w.Clubs.FirstOrDefault(c => c.Id == edited.Id);
    if (club == null) return Results.NotFound();
    club.Name = edited.Name.Trim(); club.ShortName = edited.ShortName.Trim();
    club.PrimaryColorHex = edited.PrimaryColorHex; club.SecondaryColorHex = edited.SecondaryColorHex;
    club.Stadium.Name = edited.StadiumName.Trim();
    w.RoundVersion++;
    await repo.SaveWorldAsync(w, ct);
    return Results.Ok(new { success = true });
});

app.MapPost("/api/worlds/{worldId:guid}/editor/create-club", async (
    Guid worldId, ClubExportDto edited, IWorldRepository repo, CancellationToken ct) =>
{
    var w = await repo.GetWorldAsync(worldId, ct);
    if (w == null) return Results.NotFound();
    if (w.CatalogOrigin != null || w.CustomAssemblyOrigin != null || w.HumanCoaches.Count > 0 || w.SeasonNumber > 1 || w.RoundNumber > 1)
        return Results.Conflict(new { error = "Edite uma montagem em CRIAR NOVO CAMPEONATO. Saves confirmados não permitem edição direta." });
    if (w.RoundState != RoundState.PREPARING || w.RoundNumber != 1 || w.HumanCoaches.Any(h => h.IsReadyForRound))
        return Results.BadRequest(new { error = "Crie clubes antes da primeira rodada, com todos sem pronto." });
    if (string.IsNullOrWhiteSpace(edited.Name)) return Results.BadRequest(new { error = "Informe um nome." });
    var source = WorldSeedFactory.CreateSeededWorld(seed: Random.Shared.Next(1, int.MaxValue), totalClubs: 64);
    var club = source.Clubs.First(c => c.DivisionTier == DivisionTier.Distrital);
    club.GameWorldId = w.Id; club.Name = edited.Name.Trim(); club.ShortName = edited.ShortName;
    club.PrimaryColorHex = edited.PrimaryColorHex; club.SecondaryColorHex = edited.SecondaryColorHex;
    club.Stadium.Name = edited.StadiumName; club.DeterministicIndex = w.Clubs.Max(c => c.DeterministicIndex) + 1;
    w.Clubs.Add(club);
    foreach (var p in source.Players.Where(p => p.ClubId == club.Id)) { p.GameWorldId = w.Id; w.Players.Add(p); }
    foreach (var m in source.AiManagers.Where(m => m.Id == club.AiManagerId)) { m.GameWorldId = w.Id; w.AiManagers.Add(m); }
    w.TotalClubs = w.Clubs.Count;
    CompetitionAndSeasonEngine.InitializeSeasonFixtures(w);
    w.RoundVersion++;
    await repo.SaveWorldAsync(w, ct);
    return Results.Ok(new { success = true, club.Id });
});

// 3. Synchronized Preparation (0/N Ready Counter) & Match Controls
app.MapPost("/api/worlds/{worldId:guid}/tactics", async (
    Guid worldId,
    SubmitTacticsRequest req,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var fixedReq = req with { GameWorldId = worldId };
    var (ok, err, counter, started) = await orchestrator.SubmitTacticsAsync(fixedReq, ct);
    if (!ok) return Results.BadRequest(new { error = err, counter });
    return Results.Ok(new { counter, roundStarted = started });
});

app.MapPost("/api/worlds/{worldId:guid}/ready-unemployed", async (
    Guid worldId,
    UnemployedReadyRequest req,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var fixedReq = req with { GameWorldId = worldId };
    var (ok, err, counter, started) = await orchestrator.SubmitUnemployedReadyAsync(fixedReq, ct);
    if (!ok) return Results.BadRequest(new { error = err, counter });
    return Results.Ok(new { counter, roundStarted = started });
});

app.MapPost("/api/worlds/{worldId:guid}/withdraw-ready", async (
    Guid worldId,
    WithdrawReadyRequest req,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var fixedReq = req with { GameWorldId = worldId };
    var (ok, err, counter) = await orchestrator.WithdrawReadyAsync(fixedReq, ct);
    if (!ok) return Results.BadRequest(new { error = err, counter });
    return Results.Ok(new { counter });
});

app.MapPost("/api/worlds/{worldId:guid}/pause", async (
    Guid worldId,
    RequestPauseCommand cmd,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var (ok, err, pauseWin) = await orchestrator.RequestGlobalPauseAsync(cmd with { GameWorldId = worldId }, ct);
    if (!ok) return Results.BadRequest(new { error = err, pauseWindow = pauseWin });
    return Results.Ok(pauseWin);
});

app.MapPost("/api/worlds/{worldId:guid}/penalty-taker", async (Guid worldId, ChoosePenaltyTakerCommand cmd, MultiplayerRoundOrchestrator orchestrator, CancellationToken ct) =>
{
    var (ok,err,snapshot)=await orchestrator.ChoosePenaltyTakerAsync(cmd with { GameWorldId=worldId },ct);
    return ok ? Results.Ok(snapshot) : Results.BadRequest(new { error=err,snapshot });
});

app.MapPost("/api/worlds/{worldId:guid}/in-match-adjust", async (
    Guid worldId,
    SubmitInMatchAdjustmentCommand cmd,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var (ok, err, snapshot) = await orchestrator.SubmitInMatchAdjustmentAsync(cmd with { GameWorldId = worldId }, ct);
    if (!ok) return Results.BadRequest(new { error = err, snapshot });
    return Results.Ok(snapshot);
});

app.MapPost("/api/worlds/{worldId:guid}/continue", async (
    Guid worldId,
    ConfirmContinueMatchCommand cmd,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var (ok, err, resumed, snapshot) = await orchestrator.ConfirmContinueMatchAsync(cmd with { GameWorldId = worldId }, ct);
    if (!ok) return Results.BadRequest(new { error = err, snapshot });
    return Results.Ok(new { resumed, snapshot });
});

app.MapPost("/api/worlds/{worldId:guid}/tick", async (
    Guid worldId,
    int? ticks,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    int count = Math.Clamp(ticks ?? 1, 1, 125);
    LiveRoundSnapshotDto? snap = null;
    for (int i = 0; i < count; i++)
    {
        snap = await orchestrator.AdvanceClockTickAsync(worldId, ct);
        if (snap == null || snap.RoundState is not (RoundState.RUNNING_FIRST_HALF or RoundState.RUNNING_SECOND_HALF or RoundState.RUNNING_EXTRA_TIME))
            break;
    }
    return snap == null ? Results.NotFound() : Results.Ok(snap);
});

app.MapPost("/api/worlds/{worldId:guid}/next-round", async (
    Guid worldId,
    MultiplayerRoundOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var snap = await orchestrator.AdvanceToNextRoundPreparationAsync(worldId, ct);
    return snap == null ? Results.NotFound() : Results.Ok(snap);
});

// 4. Clubs, Squads, Divisions, Cup, Top Scorers, Ranking
app.MapGet("/api/worlds/{worldId:guid}/clubs", async (
    Guid worldId,
    DivisionTier? tier,
    IWorldRepository repo,
    CancellationToken ct) =>
{
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();

    var aiDict = world.AiManagers.ToDictionary(m => m.Id);
    var humanDict = world.HumanCoaches.ToDictionary(h => h.Id);

    var clubs = world.Clubs
        .Where(c => tier == null || c.DivisionTier == tier.Value)
        .OrderBy(c => c.DivisionTier)
        .ThenBy(c => c.DeterministicIndex)
        .Select(c => new
        {
            c.Id,
            c.Name,
            c.ShortName,
            c.DivisionTier,
            DivisionDisplay = c.DivisionTier.ToDisplayPt(),
            c.ColorPalette,
            c.PrimaryColorHex,
            c.SecondaryColorHex,
            c.CashBalance,
            c.Reputation,
            c.PreferredFormation,
            IsHumanControlled = c.HumanCoachId != null,
            HumanCoachName = c.HumanCoachId != null && humanDict.TryGetValue(c.HumanCoachId.Value, out var h) ? h.Nickname : null,
            AiManagerName = c.AiManagerId != null && aiDict.TryGetValue(c.AiManagerId.Value, out var ai) ? ai.Name : null,
            AiIdentity = c.AiManagerId != null && aiDict.TryGetValue(c.AiManagerId.Value, out var ai2) ? ai2.Identity.ToString() : null,
            StadiumName = c.Stadium.Name,
            c.Stadium.BuiltCapacity,
            c.Stadium.UsableCapacity
        });

    return Results.Ok(clubs);
});

app.MapGet("/api/worlds/{worldId:guid}/clubs/{clubId:guid}", async (
    Guid worldId,
    Guid clubId,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var dto = await service.GetClubDetailAsync(worldId, clubId, ct);
    return dto == null ? Results.NotFound() : Results.Ok(dto);
});

app.MapGet("/api/worlds/{worldId:guid}/standings/{tier}", async (
    Guid worldId,
    DivisionTier tier,
    IWorldRepository repo,
    CancellationToken ct) =>
{
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();
    return Results.Ok(CompetitionAndSeasonEngine.GetDivisionStandings(world, tier));
});

app.MapGet("/api/worlds/{worldId:guid}/cup", async (Guid worldId, IWorldRepository repo, CancellationToken ct) =>
{
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();

    var clubDict = world.Clubs.ToDictionary(c => c.Id);
    var cupFixtures = world.Fixtures
        .Where(f => f.SeasonNumber == world.SeasonNumber && f.CompetitionType != CompetitionType.League)
        .OrderBy(f => f.RoundNumber)
        .ThenBy(f => f.DeterministicMatchIndex)
        .Select(f => new
        {
            f.Id,
            f.RoundNumber,
            Stage = f.CompetitionType.ToDisplayPt(),
            HomeClubId = f.HomeClubId,
            HomeClubName = clubDict[f.HomeClubId].Name,
            HomeDivision = clubDict[f.HomeClubId].DivisionTier.ToDisplayPt(),
            AwayClubId = f.AwayClubId,
            AwayClubName = clubDict[f.AwayClubId].Name,
            AwayDivision = clubDict[f.AwayClubId].DivisionTier.ToDisplayPt(),
            f.HomeGoals,
            f.AwayGoals,
            f.HomePenaltyGoals,
            f.AwayPenaltyGoals,
            f.EnteredExtraTime,
            f.IsFinished,
            WinnerClubName = f.WinnerClubId != null && clubDict.TryGetValue(f.WinnerClubId.Value, out var w) ? w.Name : null
        });

    return Results.Ok(cupFixtures);
});

app.MapGet("/api/worlds/{worldId:guid}/top-scorers", async (Guid worldId, PlayerRankingsApplicationService queries, CancellationToken ct) =>
{
    return await queries.ReadWorldAsync<IResult>(worldId, world => {
        var rows = PlayerStatisticsEngine.GetRankings(world,"scorers","general","current").Rows.Take(40);
        return Results.Ok(rows.Select(p => new TopScorerDto(p.Rank,p.PlayerId,p.DisplayName,p.Position,p.Strength,p.IsStar,
            p.CurrentClubId??Guid.Empty,p.CurrentClubName,world.Clubs.FirstOrDefault(c=>c.Id==p.CurrentClubId)?.DivisionTier??DivisionTier.Distrital,
            world.PlayerStatistics.Where(s=>s.PlayerId==p.PlayerId&&s.Season==world.SeasonNumber&&s.CompetitionType==CompetitionType.League).Sum(s=>s.GoalsScored),
            world.PlayerStatistics.Where(s=>s.PlayerId==p.PlayerId&&s.Season==world.SeasonNumber&&s.CompetitionType!=CompetitionType.League).Sum(s=>s.GoalsScored))).ToList());
    }, ct) ?? Results.NotFound();
});

app.MapGet("/api/worlds/{worldId:guid}/ranking", async (Guid worldId, IWorldRepository repo, CancellationToken ct) =>
{
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();
    return Results.Ok(new
    {
        ranking = CompetitionAndSeasonEngine.GetClassicCoachRanking(world),
        seasonHistory = world.SeasonHistories.OrderByDescending(s => s.SeasonNumber)
    });
});

// 5. Market, Auctions, Bilateral Offers, Contracts
app.MapGet("/api/worlds/{worldId:guid}/market", async (Guid worldId, ClubAndMarketApplicationService service, CancellationToken ct) =>
{
    var data=await service.GetMarketAsync(worldId,ct);
    return data==null?Results.NotFound():Results.Ok(data);
});

app.MapPost("/api/worlds/{worldId:guid}/market/list", async (
    Guid worldId,
    Guid coachId,
    Guid playerId,
    decimal askingPrice,
    string? idempotencyKey,
    long? expectedRoundVersion,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var (ok, err) = await service.ListPlayerForTransferAsync(worldId, coachId, playerId, askingPrice, ct, idempotencyKey, expectedRoundVersion);
    return ok ? Results.Ok(new { success = true, message = err }) : Results.BadRequest(new { error = err });
});

app.MapPost("/api/worlds/{worldId:guid}/market/unlist", async (
    Guid worldId,Guid coachId,Guid playerId,string? idempotencyKey,long? expectedRoundVersion,
    ClubAndMarketApplicationService service,CancellationToken ct) =>
{
    var result=await service.WithdrawTransferListingAsync(worldId,coachId,playerId,idempotencyKey??Guid.NewGuid().ToString("N"),ct,expectedRoundVersion);
    return result.Success?Results.Ok(new{success=true}):Results.BadRequest(new{error=result.Error});
});

app.MapPost("/api/worlds/{worldId:guid}/market/auction", async (
    Guid worldId,CreateVoluntaryAuctionCommandDto command,ClubAndMarketApplicationService service,CancellationToken ct) =>
{
    var result=await service.CreateVoluntaryAuctionAsync(worldId,command.CoachId,command.PlayerId,command.MinimumBid,command.DurationRounds,ct,command.IdempotencyKey,command.ExpectedRoundVersion);
    return result.Success?Results.Ok(new{success=true,auctionId=result.AuctionId}):Results.BadRequest(new{error=result.Error});
});

app.MapPost("/api/worlds/{worldId:guid}/market/buy-listed", async (
    Guid worldId,
    Guid coachId,
    Guid playerId,
    string? idempotencyKey,
    long? expectedRoundVersion,
    long? listingVersion,
    Guid? sellerClubId,
    decimal? askingPrice,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var (ok, err) = await service.BuyListedPlayerAsync(worldId, coachId, playerId, idempotencyKey ?? Guid.NewGuid().ToString("N"), ct, expectedRoundVersion, listingVersion, sellerClubId, askingPrice);
    return ok ? Results.Ok(new { success = true, message = err }) : Results.BadRequest(new { error = err });
});

app.MapPost("/api/worlds/{worldId:guid}/market/offer", async (
    Guid worldId,
    Guid coachId,
    Guid playerId,
    decimal offeredAmount,
    string? idempotencyKey,
    long? expectedRoundVersion,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var (ok, err, offer) = await service.CreateTransferOfferAsync(
        worldId, coachId, playerId, offeredAmount, idempotencyKey ?? Guid.NewGuid().ToString("N"), ct, expectedRoundVersion);
    return ok ? Results.Ok(offer) : Results.BadRequest(new { error = err });
});

app.MapPost("/api/worlds/{worldId:guid}/market/offer/{offerId:guid}/respond", async (
    Guid worldId,
    Guid offerId,
    Guid coachId,
    bool accept,
    string? idempotencyKey,
    long? expectedRoundVersion,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var (ok, err) = await service.RespondToTransferOfferAsync(
        worldId, coachId, offerId, accept, idempotencyKey ?? Guid.NewGuid().ToString("N"), ct, expectedRoundVersion);
    return ok ? Results.Ok(new { success = true, message = err }) : Results.BadRequest(new { error = err });
});

app.MapPost("/api/worlds/{worldId:guid}/market/offer/{offerId:guid}/counter", async (
    Guid worldId, Guid offerId, Guid coachId, decimal amount, string? idempotencyKey,
    long? expectedRoundVersion, ClubAndMarketApplicationService service, CancellationToken ct) =>
{
    var result=await service.CounterTransferOfferAsync(worldId,coachId,offerId,amount,idempotencyKey??Guid.NewGuid().ToString("N"),ct,expectedRoundVersion);
    return result.Success?Results.Ok(new{success=true}):Results.BadRequest(new{error=result.Error});
});

app.MapPost("/api/worlds/{worldId:guid}/market/auction/{auctionId:guid}/bid", async (
    Guid worldId,
    Guid auctionId,
    Guid coachId,
    decimal bidAmount,
    string? idempotencyKey,
    long? expectedRoundVersion,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var (ok, err) = await service.PlaceAuctionBidAsync(worldId, coachId, auctionId, bidAmount, ct, idempotencyKey, expectedRoundVersion);
    return ok ? Results.Ok(new { success = true, message = err }) : Results.BadRequest(new { error = err });
});

app.MapPost("/api/worlds/{worldId:guid}/players/{playerId:guid}/renew", async (
    Guid worldId,
    Guid playerId,
    Guid coachId,
    int additionalRounds,
    string? idempotencyKey,
    long? expectedRoundVersion,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var (ok, err) = await service.RenewPlayerContractAsync(worldId, coachId, playerId, additionalRounds, ct, idempotencyKey, expectedRoundVersion);
    return ok ? Results.Ok(new { success = true, message = err }) : Results.BadRequest(new { error = err });
});

// 6. Finances, Loans & Stadium Works
app.MapGet("/api/worlds/{worldId:guid}/clubs/{clubId:guid}/finances", async (
    Guid worldId,
    Guid clubId,
    IWorldRepository repo,
    CancellationToken ct) =>
{
    var gate=MultiplayerRoundOrchestrator.GetRoomLock(worldId);await gate.WaitAsync(ct);
    try {
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();

    var club = world.Clubs.FirstOrDefault(c => c.Id == clubId);
    if (club == null) return Results.NotFound();

    ClassicEconomyEngine.EnsureMigrated(world);
    var credit=ClassicEconomyEngine.AssessCredit(world,club);
    var ledger = world.Transactions
        .Where(t => t.ClubId == clubId)
        .OrderByDescending(t => t.CreatedAtUtc)
        .Take(100)
        .Select(t => new
        {
            t.Id,
            t.SeasonNumber,
            t.RoundNumber,
            Category = t.Category.ToString(),
            t.Amount,
            t.BalanceAfter,
            t.Description,
            t.CreatedAtUtc
        });

    var loans = world.BankLoans.Where(l => l.ClubId == clubId);
    return Results.Ok(System.Text.Json.JsonSerializer.SerializeToElement(new
    {
        clubId = club.Id,
        clubName = club.Name,
        cashBalance = club.CashBalance,
        availableCash = ClassicEconomyEngine.AvailableCash(world,club),
        annualSponsorshipBudget = world.Economy.OperatingRules.AnnualSponsorship
            ?world.Economy.SponsorshipBudgets.GetValueOrDefault($"S{world.SeasonNumber}:{club.Id}")?.AnnualBudget??OperatingEconomyEngine.CommercialBase(club.DivisionTier)
            :(decimal?)null,
        sponsorshipInstallments=world.Economy.OperatingRules.AnnualSponsorship?world.TotalRoundsInSeason:(int?)null,
        salaryPeriod="OFFICIAL_MATCH",
        commercialChangeFromSeason=world.Economy.OperatingUpgradeSeason,
        renovationCostEstimate = MarketAndEconomyEngine.CalculateRenovationCost(club.Stadium),
        expansion5kCostEstimate = MarketAndEconomyEngine.CalculateExpansionCost(club.Stadium, 5_000),
        ledger,
        loans,
        credit,
        roundVersion=world.RoundVersion,
        salaryRequests=world.Economy.SalaryRequests.Where(r=>r.ClubId==clubId).OrderByDescending(r=>r.Season).ThenByDescending(r=>r.Round).ToList(),
        contracts=world.Economy.ContractHistory.Where(r=>r.ClubId==clubId).ToList(),
        insolvency=world.Economy.Crises.GetValueOrDefault(clubId),
        negativeCash=new {
            principal=Math.Max(0,-club.CashBalance),
            interestRatePerRound=world.Economy.Rules.NegativeCashInterestPerRound,
            estimatedNextRoundInterest=Math.Round(Math.Max(0,-club.CashBalance)*world.Economy.Rules.NegativeCashInterestPerRound,2)
        },
        coverageWarnings=world.Economy.CoverageWarnings
    },new System.Text.Json.JsonSerializerOptions{PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.CamelCase}));
    } finally {gate.Release();}
});

app.MapPost("/api/worlds/{worldId:guid}/clubs/{clubId:guid}/loans", async (
    Guid worldId,
    Guid clubId,
    Guid coachId,
    decimal amount,
    int rounds,
    string? idempotencyKey,
    long? expectedRoundVersion,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var (ok, err) = await service.TakeBankLoanAsync(worldId, coachId, amount, rounds, ct, idempotencyKey, expectedRoundVersion, clubId);
    return ok ? Results.Ok(new { success = true, message = err }) : Results.BadRequest(new { error = err });
});

app.MapPost("/api/worlds/{worldId:guid}/clubs/{clubId:guid}/stadium/renovate", async (
    Guid worldId,Guid clubId,Guid coachId,string? idempotencyKey,long? expectedRoundVersion,ClubAndMarketApplicationService service,CancellationToken ct) =>
{
    var (ok,err)=await service.StadiumCommandAsync(worldId,coachId,clubId,false,0,idempotencyKey??Guid.NewGuid().ToString("N"),ct,expectedRoundVersion);
    return ok?Results.Ok(new{success=true}):Results.BadRequest(new{error=err});
});
app.MapPost("/api/worlds/{worldId:guid}/clubs/{clubId:guid}/stadium/expand", async (
    Guid worldId,Guid clubId,Guid coachId,int additionalSeats,string? idempotencyKey,long? expectedRoundVersion,ClubAndMarketApplicationService service,CancellationToken ct) =>
{
    var (ok,err)=await service.StadiumCommandAsync(worldId,coachId,clubId,true,additionalSeats,idempotencyKey??Guid.NewGuid().ToString("N"),ct,expectedRoundVersion);
    return ok?Results.Ok(new{success=true}):Results.BadRequest(new{error=err});
});
app.MapPost("/api/worlds/{worldId:guid}/clubs/{clubId:guid}/loans/{loanId:guid}/repay", async (
    Guid worldId,Guid clubId,Guid loanId,FinancialCommandDto command,ClubAndMarketApplicationService service,CancellationToken ct)=>
{
    var(ok,err)=await service.RepayBankLoanAsync(worldId,command.CoachId,clubId,loanId,command.IdempotencyKey,ct,command.ExpectedRoundVersion);
    return ok?Results.Ok(new{success=true}):Results.BadRequest(new{error=err});
});
app.MapPost("/api/worlds/{worldId:guid}/clubs/{clubId:guid}/salary-requests/{requestId:guid}/respond", async (
    Guid worldId,Guid clubId,Guid requestId,SalaryDecisionCommandDto command,ClubAndMarketApplicationService service,CancellationToken ct)=>
{
    var(ok,err)=await service.RespondSalaryAsync(worldId,command.CoachId,clubId,requestId,command.Accept,command.IdempotencyKey,ct,command.ExpectedRoundVersion);
    return ok?Results.Ok(new{success=true,message=err}):Results.BadRequest(new{error=err});
});

// 7. Career & Job Invitations
app.MapGet("/api/worlds/{worldId:guid}/invitations/{coachId:guid}", async (
    Guid worldId,
    Guid coachId,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var career = await service.GetCareerAsync(worldId, coachId, ct);
    return career == null ? Results.NotFound() : Results.Ok(career);
});

app.MapPost("/api/worlds/{worldId:guid}/invitations/{invitationId:guid}/accept", async (
    Guid worldId,
    Guid invitationId,
    Guid coachId,
    ClubAndMarketApplicationService service,
    CancellationToken ct) =>
{
    var (ok, err) = await service.AcceptJobInvitationAsync(worldId, coachId, invitationId, ct);
    return ok ? Results.Ok(new { success = true, message = err }) : Results.BadRequest(new { error = err });
});

app.MapPost("/api/worlds/{worldId:guid}/invitations/{invitationId:guid}/reject", async (
    Guid worldId, Guid invitationId, Guid coachId, ClubAndMarketApplicationService service, CancellationToken ct) =>
{
    var (ok, err) = await service.RespondJobInvitationAsync(worldId, coachId, invitationId, false, ct);
    return ok ? Results.Ok(new { success = true, message = err }) : Results.BadRequest(new { error = err });
});

app.MapPut("/api/worlds/{worldId:guid}/career/morale-rules", async (
    Guid worldId, Guid coachId, CoachMoraleRules rules, ClubAndMarketApplicationService service, CancellationToken ct) =>
{
    var (ok,err) = await service.UpdateMoraleRulesAsync(worldId,coachId,rules,ct);
    return ok ? Results.Ok(new { success=true }) : Results.BadRequest(new { error=err });
});

// 8. Editor & Data Packages (JSON / CSV Import & Export)
app.MapGet("/api/worlds/{worldId:guid}/editor/export-json", async (Guid worldId, IWorldRepository repo, CancellationToken ct) =>
{
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();
    if (world.CatalogOrigin?.References.Any(r => !r.IsFixture && r.Rights?.WorldExportAllowed != true) == true) return Results.StatusCode(403);
    var pkg = WorldEditorAndPackageService.ExportWorldToPackage(world, world.Name);
    return Results.Ok(pkg);
});

app.MapGet("/api/worlds/{worldId:guid}/editor/export-csv", async (Guid worldId, IWorldRepository repo, CancellationToken ct) =>
{
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();
    if (world.CatalogOrigin?.References.Any(r => !r.IsFixture && r.Rights?.WorldExportAllowed != true) == true) return Results.StatusCode(403);
    string csv = WorldEditorAndPackageService.ExportPlayersToCsv(world);
    return Results.Text(csv, "text/csv", Encoding.UTF8);
});

app.MapPost("/api/worlds/{worldId:guid}/editor/validate-package", async (
    Guid worldId,
    WorldPackageExportDto package,
    IWorldRepository repo,
    CancellationToken ct) =>
{
    var baseline = await repo.GetWorldAsync(worldId, ct);
    var res = WorldEditorAndPackageService.ValidatePackage(package, baseline);
    return Results.Ok(res);
});

app.MapPost("/api/worlds/editor/import-package", async (
    WorldPackageExportDto package,
    string? newWorldName,
    IWorldRepository repo,
    CancellationToken ct) =>
{
    var (newWorld, validation) = WorldEditorAndPackageService.CreateWorldFromPackage(
        package, string.IsNullOrWhiteSpace(newWorldName) ? $"{package.PackageName} (Editado)" : newWorldName.Trim());

    if (!validation.IsValid || newWorld == null)
        return Results.BadRequest(validation);

    await repo.SaveWorldAsync(newWorld, ct);
    return Results.Ok(new
    {
        worldId = newWorld.Id,
        inviteCode = newWorld.InviteCode,
        validation
    });
});

app.MapPost("/api/worlds/{worldId:guid}/editor/update-player", async (
    Guid worldId,
    PlayerExportDto edited,
    IWorldRepository repo,
    CancellationToken ct) =>
{
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();
    if (world.CatalogOrigin != null || world.CustomAssemblyOrigin != null || world.HumanCoaches.Count > 0 || world.SeasonNumber > 1 || world.RoundNumber > 1)
        return Results.Conflict(new { error = "Edite uma montagem em CRIAR NOVO CAMPEONATO. Saves confirmados não permitem edição direta." });
    if (world.RoundState != RoundState.PREPARING)
        return Results.BadRequest(new { error = "Edição direta só é permitida com a rodada em PREPARING (ou crie uma cópia do mundo)." });
    if (world.HumanCoaches.Any(h => h.IsReadyForRound))
        return Results.BadRequest(new { error = "Retire pronto antes de editar jogadores." });

    if (edited.Strength < 1 || edited.Strength > 50)
        return Results.BadRequest(new { error = "A força do jogador deve ser um inteiro entre 1 e 50." });
    if (string.IsNullOrWhiteSpace(edited.Name) || !Enum.IsDefined(edited.Position) || !Enum.IsDefined(edited.Behavior))
        return Results.BadRequest(new { error = "Informe nome, posição e comportamento válidos." });

    var player = world.Players.FirstOrDefault(p => p.Id == edited.Id);
    if (player == null) return Results.NotFound(new { error = "Jogador não encontrado." });

    if (player.Position == PlayerPosition.G && edited.Position != PlayerPosition.G && player.ClubId != null)
    {
        var squad = world.Players.Where(p => p.ClubId == player.ClubId).ToList();
        if (squad.Count(p => p.Position == PlayerPosition.G) <= 1)
            return Results.BadRequest(new { error = "Não é possível remover a posição G do único goleiro do elenco." });
    }

    player.Name = edited.Name.Trim();
    player.Position = edited.Position;
    player.Strength = edited.Strength;
    player.Behavior = edited.Behavior;
    player.IsStar = edited.IsStar;
    player.ContractRoundsRemaining = Math.Max(1, edited.ContractRoundsRemaining);
    player.SalaryPerRound = Math.Max(0m, edited.SalaryPerRound);
    player.MarketPrice = edited.MarketPrice > 0 ? edited.MarketPrice : MarketAndEconomyEngine.CalculateFairValue(player);
    player.PhotoUrl = edited.PhotoUrl;
    player.PhotoLicense = edited.PhotoLicense;
    player.PhotoCredit = edited.PhotoCredit;

    world.RoundVersion++;
    await repo.SaveWorldAsync(world, ct);
    return Results.Ok(ClubAndMarketApplicationService.MapPlayerDto(player, null));
});

app.MapPost("/api/worlds/{worldId:guid}/editor/update-manager", async (
    Guid worldId,
    AiManagerExportDto edited,
    IWorldRepository repo,
    CancellationToken ct) =>
{
    var world = await repo.GetWorldAsync(worldId, ct);
    if (world == null) return Results.NotFound();
    if (world.CatalogOrigin != null || world.CustomAssemblyOrigin != null || world.HumanCoaches.Count > 0 || world.SeasonNumber > 1 || world.RoundNumber > 1)
        return Results.Conflict(new { error = "Edite uma montagem em CRIAR NOVO CAMPEONATO. Saves confirmados não permitem edição direta." });

    int weightSum = edited.PreferredFormations.Sum(f => f.WeightPercent);
    if (world.RoundState != RoundState.PREPARING || world.HumanCoaches.Any(h => h.IsReadyForRound))
        return Results.BadRequest(new { error = "Edite técnicos somente na preparação, com todos sem pronto." });
    if (string.IsNullOrWhiteSpace(edited.Name) || !Enum.IsDefined(edited.Identity) ||
        edited.PreferredFormations.Count is < 1 or > 3 || edited.PreferredFormations.Any(p => p.WeightPercent <= 0 ||
            !TacticalFormations.TryGet(p.Formation, world.AllowClassic505, out _)))
        return Results.BadRequest(new { error = "Informe nome e de uma a três formações válidas com pesos positivos." });
    if (weightSum != 100)
        return Results.BadRequest(new { error = $"Os pesos das táticas preferidas devem somar 100% (atual: {weightSum}%)." });

    var mgr = world.AiManagers.FirstOrDefault(m => m.Id == edited.Id);
    if (mgr == null) return Results.NotFound();

    mgr.Name = edited.Name.Trim();
    mgr.Identity = edited.Identity;
    mgr.Pragmatism = Math.Clamp(edited.Pragmatism, 0, 100);
    mgr.Adaptability = Math.Clamp(edited.Adaptability, 0, 100);
    mgr.FinancialAggressiveness = Math.Clamp(edited.FinancialAggressiveness, 0, 100);
    mgr.YouthPatience = Math.Clamp(edited.YouthPatience, 0, 100);
    mgr.PreferredFormations = edited.PreferredFormations;

    await repo.SaveWorldAsync(world, ct);
    return Results.Ok(mgr);
});

app.Run();

public partial class Program { }
