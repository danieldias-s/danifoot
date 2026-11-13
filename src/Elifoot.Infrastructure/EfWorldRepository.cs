using System.Collections.Concurrent;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Infrastructure;

public sealed class EfWorldRepository : IWorldRepository
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly ConcurrentDictionary<Guid, GameWorld> MemoryCache = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SaveLocks = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public EfWorldRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public static void ClearCacheForTesting() => MemoryCache.Clear();

    public async Task<GameWorld?> GetWorldAsync(Guid worldId, CancellationToken ct = default)
    {
        if (MemoryCache.TryGetValue(worldId, out var cached))
        {
            return cached;
        }

        var loadGate=SaveLocks.GetOrAdd(worldId,_=>new SemaphoreSlim(1,1));
        await loadGate.WaitAsync(ct);
        try {
        if(MemoryCache.TryGetValue(worldId,out cached))return cached;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        var record = await db.Worlds.AsNoTracking().FirstOrDefaultAsync(w => w.Id == worldId, ct);
        if (record == null) return null;

        var world = JsonSerializer.Deserialize<GameWorld>(record.SnapshotJson, JsonOptions);
        if (world != null)
        {
            CoachCareerEngine.EnsureMigrated(world);
            PlayerStatisticsEngine.EnsureMigrated(world);
            DisciplineEngine.EnsureMigrated(world);
            ClassicEconomyEngine.EnsureMigrated(world);
            OperatingEconomyEngine.EnsureScheduledRules(world);
            HistoryEngine.Observe(world);
            MemoryCache[world.Id] = world;
        }
        return world;
        } finally {loadGate.Release();}
    }

    public async Task<GameWorld?> GetWorldByInviteCodeAsync(string inviteCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inviteCode)) return null;
        string normalized = inviteCode.Trim();

        var fromMem = MemoryCache.Values.FirstOrDefault(w =>
            string.Equals(w.InviteCode, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.Id.ToString(), normalized, StringComparison.OrdinalIgnoreCase));
        if (fromMem != null) return fromMem;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        Guid? foundId = null;
        if (Guid.TryParse(normalized, out var parsedId))
        {
            foundId = await db.Worlds.AsNoTracking().Where(w=>w.Id==parsedId).Select(w=>(Guid?)w.Id).FirstOrDefaultAsync(ct);
        }
        foundId ??= await db.Worlds.AsNoTracking().Where(w=>w.InviteCode.ToUpper()==normalized.ToUpper()).Select(w=>(Guid?)w.Id).FirstOrDefaultAsync(ct);
        return foundId is Guid id?await GetWorldAsync(id,ct):null;
    }

    public async Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        // The room list needs identities first. Warm worlds must not download huge snapshots again.
        var ids = await db.Worlds.AsNoTracking().OrderByDescending(w => w.UpdatedAtUtc).Select(w=>w.Id).ToListAsync(ct);

        var result = new List<GameWorld>(ids.Count);
        foreach (var id in ids)
        {
            var world=await GetWorldAsync(id,ct);
            if(world!=null)result.Add(world);
        }
        return result;
    }

    public Task SaveWorldAsync(GameWorld world, CancellationToken ct = default)
        => SaveWorldAsync(world, ct, previousPauseVersion: null);

    public Task SavePauseTransitionAsync(GameWorld world, long previousVersion)
        => SaveWorldAsync(world, CancellationToken.None, previousVersion);

    private async Task SaveWorldAsync(GameWorld world, CancellationToken ct, long? previousPauseVersion)
    {
        var gate = SaveLocks.GetOrAdd(world.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try {
        world.UpdatedAtUtc = DateTime.UtcNow;
        RuntimePlayerProvenance.EnsureGeneratedPlayers(world);

        // Avoid serializing full snapshot on every intermediate live match minute except checkpoints (0, 45, 90, 105, 120) or paused/preparing states
        bool isCheckpointState = world.RoundState is RoundState.PREPARING
            or RoundState.ALL_READY
            or RoundState.GLOBAL_PAUSED
            or RoundState.HALFTIME_PAUSED
            or RoundState.EXTRA_TIME_BREAK
            or RoundState.EXTRA_TIME_HALFTIME
            or RoundState.RESULTS
            or RoundState.FINISHED
            || world.CurrentMatchMinute % 15 == 0
            || world.RequiresDurableMatchInteractionSave
            || world.Fixtures.Any(m => m.PendingPenalties.Count > 0);

        if (!isCheckpointState) { MemoryCache[world.Id] = world; return; }
        HistoryEngine.Observe(world);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();

        string json = JsonSerializer.Serialize(world, JsonOptions);
        // Track the actual concurrency token and scalar header without reading SnapshotJson.
        var existing = await db.Worlds.AsNoTracking().Where(w=>w.Id==world.Id).Select(w=>new WorldRecord {
            Id=w.Id,Name=w.Name,InviteCode=w.InviteCode,Seed=w.Seed,TotalClubs=w.TotalClubs,
            SeasonNumber=w.SeasonNumber,RoundNumber=w.RoundNumber,RoundState=w.RoundState,RoundVersion=w.RoundVersion,
            CurrentMatchMinute=w.CurrentMatchMinute,CreatedAtUtc=w.CreatedAtUtc,UpdatedAtUtc=w.UpdatedAtUtc
        }).FirstOrDefaultAsync(ct);
        if (previousPauseVersion is long expected && existing?.RoundVersion != expected)
            throw new DbUpdateConcurrencyException("The persisted pause has changed.");
        if (existing == null)
        {
            db.Worlds.Add(new WorldRecord
            {
                Id = world.Id,
                Name = world.Name,
                InviteCode = world.InviteCode,
                Seed = world.Seed,
                TotalClubs = world.TotalClubs,
                SeasonNumber = world.SeasonNumber,
                RoundNumber = world.RoundNumber,
                RoundState = (int)world.RoundState,
                RoundVersion = world.RoundVersion,
                CurrentMatchMinute = world.CurrentMatchMinute,
                SnapshotJson = json,
                CreatedAtUtc = world.CreatedAtUtc,
                UpdatedAtUtc = world.UpdatedAtUtc
            });
        }
        else
        {
            db.Worlds.Attach(existing);
            existing.Name = world.Name;
            existing.InviteCode = world.InviteCode;
            existing.SeasonNumber = world.SeasonNumber;
            existing.RoundNumber = world.RoundNumber;
            existing.RoundState = (int)world.RoundState;
            existing.RoundVersion = world.RoundVersion;
            existing.CurrentMatchMinute = world.CurrentMatchMinute;
            existing.SnapshotJson = json;
            existing.UpdatedAtUtc = world.UpdatedAtUtc;
        }

        // Sync relational summary tables when in PREPARING or RESULTS
        if (world.RoundState is RoundState.PREPARING or RoundState.RESULTS)
        {
            var existingLedgerIds = await db.FinancialLedger
                .Where(l => l.GameWorldId == world.Id)
                .Select(l => l.Id)
                .ToHashSetAsync(ct);

            var newTransactions = world.Transactions
                .Where(t => !existingLedgerIds.Contains(t.Id))
                .Select(t => new FinancialLedgerRecord
                {
                    Id = t.Id,
                    GameWorldId = world.Id,
                    ClubId = t.ClubId,
                    SeasonNumber = t.SeasonNumber,
                    RoundNumber = t.RoundNumber,
                    Category = (int)t.Category,
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    Description = t.Description,
                    IdempotencyKey = t.IdempotencyKey,
                    CreatedAtUtc = t.CreatedAtUtc
                });
            db.FinancialLedger.AddRange(newTransactions);

            var existingHistoryIds = await db.SeasonHistories
                .Where(h => h.GameWorldId == world.Id)
                .Select(h => h.Id)
                .ToHashSetAsync(ct);

            foreach (var h in world.SeasonHistories.Where(x => !existingHistoryIds.Contains(x.Id)))
            {
                db.SeasonHistories.Add(new SeasonHistoryRecord
                {
                    Id = h.Id,
                    GameWorldId = world.Id,
                    SeasonNumber = h.SeasonNumber,
                    FirstDivisionChampionClubId = h.FirstDivisionChampionClubId,
                    FirstDivisionChampionName = h.FirstDivisionChampionName,
                    CupChampionClubId = h.CupChampionClubId,
                    CupChampionName = h.CupChampionName,
                    TopScorerPlayerName = h.TopScorerPlayerName,
                    TopScorerGoals = h.TopScorerGoals
                });
            }
        }

        await db.SaveChangesAsync(ct);
        // A failed first save must never make an unpersisted world visible to confirmation retries.
        MemoryCache[world.Id] = world;
        world.RequiresDurableMatchInteractionSave = false;
        }
        catch when (previousPauseVersion.HasValue)
        {
            // A failed write may have committed. The next command must read the persisted
            // snapshot and receipt before deciding whether to apply the transition.
            MemoryCache.TryRemove(world.Id, out _);
            throw;
        }
        finally { gate.Release(); }
    }
}

public sealed class InMemoryWorldRepository : IWorldRepository
{
    private readonly ConcurrentDictionary<Guid, GameWorld> _worlds = new();

    public Task<GameWorld?> GetWorldAsync(Guid worldId, CancellationToken ct = default)
        => Task.FromResult(_worlds.GetValueOrDefault(worldId));

    public Task<GameWorld?> GetWorldByInviteCodeAsync(string inviteCode, CancellationToken ct = default)
    {
        var found = _worlds.Values.FirstOrDefault(w =>
            string.Equals(w.InviteCode, inviteCode?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.Id.ToString(), inviteCode?.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(found);
    }

    public Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct = default)
        => Task.FromResult(_worlds.Values.OrderByDescending(w => w.UpdatedAtUtc).ToList());

    public Task SaveWorldAsync(GameWorld world, CancellationToken ct = default)
    {
        if(CoachCareerEngine.IsSafe(world))HistoryEngine.Observe(world);
        world.UpdatedAtUtc = DateTime.UtcNow;
        _worlds[world.Id] = world;
        return Task.CompletedTask;
    }
}
