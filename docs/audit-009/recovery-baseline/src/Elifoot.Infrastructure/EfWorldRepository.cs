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

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        var record = await db.Worlds.AsNoTracking().FirstOrDefaultAsync(w => w.Id == worldId, ct);
        if (record == null) return null;

        var world = JsonSerializer.Deserialize<GameWorld>(record.SnapshotJson, JsonOptions);
        if (world != null)
        {
            CoachCareerEngine.EnsureMigrated(world);
            PlayerStatisticsEngine.EnsureMigrated(world);
            MemoryCache[world.Id] = world;
        }
        return world;
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
        WorldRecord? record = null;
        if (Guid.TryParse(normalized, out var parsedId))
        {
            record = await db.Worlds.AsNoTracking().FirstOrDefaultAsync(w => w.Id == parsedId, ct);
        }
        record ??= await db.Worlds.AsNoTracking()
            .FirstOrDefaultAsync(w => w.InviteCode.ToUpper() == normalized.ToUpper(), ct);

        if (record == null) return null;

        var world = JsonSerializer.Deserialize<GameWorld>(record.SnapshotJson, JsonOptions);
        if (world != null)
        {
            CoachCareerEngine.EnsureMigrated(world);
            PlayerStatisticsEngine.EnsureMigrated(world);
            MemoryCache[world.Id] = world;
        }
        return world;
    }

    public async Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        var records = await db.Worlds.AsNoTracking().OrderByDescending(w => w.UpdatedAtUtc).ToListAsync(ct);

        var result = new List<GameWorld>(records.Count);
        foreach (var r in records)
        {
            if (MemoryCache.TryGetValue(r.Id, out var mem))
            {
                result.Add(mem);
            }
            else
            {
                var w = JsonSerializer.Deserialize<GameWorld>(r.SnapshotJson, JsonOptions);
                if (w != null)
                {
                    CoachCareerEngine.EnsureMigrated(w);
                    PlayerStatisticsEngine.EnsureMigrated(w);
                    MemoryCache[w.Id] = w;
                    result.Add(w);
                }
            }
        }
        return result;
    }

    public async Task SaveWorldAsync(GameWorld world, CancellationToken ct = default)
    {
        var gate = SaveLocks.GetOrAdd(world.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try {
        world.UpdatedAtUtc = DateTime.UtcNow;
        MemoryCache[world.Id] = world;

        // Avoid serializing full snapshot on every intermediate live match minute except checkpoints (0, 45, 90, 105, 120) or paused/preparing states
        bool isCheckpointState = world.RoundState is RoundState.PREPARING
            or RoundState.ALL_READY
            or RoundState.GLOBAL_PAUSED
            or RoundState.HALFTIME_PAUSED
            or RoundState.EXTRA_TIME_BREAK
            or RoundState.EXTRA_TIME_HALFTIME
            or RoundState.RESULTS
            or RoundState.FINISHED
            || world.CurrentMatchMinute % 15 == 0;

        if (!isCheckpointState) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();

        string json = JsonSerializer.Serialize(world, JsonOptions);
        var existing = await db.Worlds.FirstOrDefaultAsync(w => w.Id == world.Id, ct);
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
        } finally { gate.Release(); }
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
        world.UpdatedAtUtc = DateTime.UtcNow;
        _worlds[world.Id] = world;
        return Task.CompletedTask;
    }
}
