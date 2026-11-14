using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    private readonly WorldAuthority? _authority;
    private readonly SqliteSnapshotOptions? _snapshotOptions;
    private readonly WorldPersistenceMetrics? _metrics;
    private readonly ConditionalWeakTable<GameWorld, SnapshotFormat> _snapshotFormats = new();
    private sealed record SnapshotFormat(bool Compressed);
    private static readonly ConcurrentDictionary<Guid, WeakReference<GameWorld>> WorldIdentity = new();
    private static readonly ConcurrentDictionary<Guid, (GameWorld World, long Access)> MemoryCache = new();
    // A fixed set bounds metadata for worlds seen over the lifetime of a server.
    private static readonly SemaphoreSlim[] SaveLocks = Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly WorldCacheOptions _cacheOptions;
    private long _lastSweep;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public EfWorldRepository(IServiceScopeFactory scopeFactory, WorldAuthority? authority = null,
        SqliteSnapshotOptions? snapshotOptions = null, WorldCacheOptions? cacheOptions = null,
        WorldPersistenceMetrics? metrics = null)
    {
        _scopeFactory = scopeFactory;
        _authority = authority;
        _snapshotOptions = snapshotOptions;
        _metrics = metrics;
        _cacheOptions = cacheOptions ?? new(16, TimeSpan.FromMinutes(2));
        if (_cacheOptions.InactiveCount < 0 || _cacheOptions.InactiveLifetime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cacheOptions));
    }

    public static void ClearCacheForTesting()
    {
        MemoryCache.Clear();
        WorldIdentity.Clear();
    }

    public static (int Retained, int Identities) CacheCounts => (MemoryCache.Count, WorldIdentity.Count);

    private static bool IsPinned(GameWorld world) =>
        IsClockState(world.RoundState) || world.RoundState == RoundState.ALL_READY ||
        world.RoundCheckpointNeedsSave || world.PendingWorldDataChange != null ||
        world.RequiresDurableMatchInteractionSave || world.PersistedRoundVersion != world.RoundVersion;

    private bool TryGetWorld(Guid id, out GameWorld? world)
    {
        if (WorldIdentity.TryGetValue(id, out var reference) && reference.TryGetTarget(out world))
        {
            long now = Environment.TickCount64;
            if (MemoryCache.TryGetValue(id, out var entry) && ReferenceEquals(entry.World, world))
                MemoryCache.TryUpdate(id, (world, now), entry);
            Sweep(now);
            return true;
        }
        world = null;
        return false;
    }

    private void Remember(GameWorld world)
    {
        var now = Environment.TickCount64;
        // The weak identity preserves the same aggregate while any command or reader holds it.
        WorldIdentity.AddOrUpdate(world.Id, _ => new(world), (_, existing) =>
            existing.TryGetTarget(out var current) && ReferenceEquals(world, current) ? existing : new(world));
        if (_cacheOptions.InactiveCount == 0 && !IsPinned(world))
            MemoryCache.TryRemove(world.Id, out _);
        else
            MemoryCache[world.Id] = (world, now);
        Sweep(now);
    }

    private void Sweep(long now)
    {
        var previous = Interlocked.Read(ref _lastSweep);
        if (now - previous < 1000 || Interlocked.CompareExchange(ref _lastSweep, now, previous) != previous) return;
        var inactive = MemoryCache.Where(x => !IsPinned(x.Value.World))
            .OrderByDescending(x => x.Value.Access).ToArray();
        for (int i = 0; i < inactive.Length; i++)
        {
            var entry = inactive[i];
            if ((i >= _cacheOptions.InactiveCount || now - entry.Value.Access >= _cacheOptions.InactiveLifetime.TotalMilliseconds)
                && !IsPinned(entry.Value.World))
                MemoryCache.TryRemove(entry);
        }
        foreach (var entry in WorldIdentity)
            if (!entry.Value.TryGetTarget(out _)) WorldIdentity.TryRemove(entry);
    }

    public async Task<GameWorld?> GetWorldAsync(Guid worldId, CancellationToken ct = default)
    {
        var world = await GetWorldForRoundCheckpointAsync(worldId, ct);
        if (world?.RoundCheckpointNeedsSave == true || world?.PendingWorldDataChange != null)
            throw new RoundCheckpointPendingException();
        return world;
    }

    public async Task<GameWorld?> GetWorldForRoundCheckpointAsync(Guid worldId, CancellationToken ct = default)
    {
        if (TryGetWorld(worldId, out var cached))
        {
            return cached;
        }

        var loadGate = SaveLocks[(uint)worldId.GetHashCode() % SaveLocks.Length];
        await loadGate.WaitAsync(ct);
        try {
        if(TryGetWorld(worldId,out cached))return cached;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        var record = await db.Worlds.AsNoTracking().FirstOrDefaultAsync(w => w.Id == worldId, ct);
        if (record == null) return null;

        var hydrationStart = Stopwatch.GetTimestamp();
        var world = await WorldSnapshotCodec.ReadAsync(record.SnapshotJson, JsonOptions, ct);
        _metrics?.Hydration.Record(hydrationStart, record.SnapshotJson.Length);
        if (world != null)
        {
            if (db.Database.IsSqlite())
                _snapshotFormats.AddOrUpdate(world, new(WorldSnapshotCodec.IsCompressed(record.SnapshotJson)));
            world.PersistedRoundVersion = record.RoundVersion;
            CoachCareerEngine.EnsureMigrated(world);
            PlayerStatisticsEngine.EnsureMigrated(world);
            DisciplineEngine.EnsureMigrated(world);
            ClassicEconomyEngine.EnsureMigrated(world);
            OperatingEconomyEngine.EnsureScheduledRules(world);
            HistoryEngine.Observe(world);
            Remember(world);
        }
        return world;
        } finally {loadGate.Release();}
    }

    public async Task<GameWorld?> GetWorldByInviteCodeAsync(string inviteCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inviteCode)) return null;
        string normalized = inviteCode.Trim();

        var fromMem = MemoryCache.Values.Select(e => e.World).FirstOrDefault(w =>
            string.Equals(w.InviteCode, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.Id.ToString(), normalized, StringComparison.OrdinalIgnoreCase));
        if (fromMem != null) return await GetWorldForRoundCheckpointAsync(fromMem.Id, ct);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        Guid? foundId = null;
        if (Guid.TryParse(normalized, out var parsedId))
        {
            foundId = await db.Worlds.AsNoTracking().Where(w=>w.Id==parsedId).Select(w=>(Guid?)w.Id).FirstOrDefaultAsync(ct);
        }
        foundId ??= await db.Worlds.AsNoTracking().Where(w=>w.InviteCode.ToUpper()==normalized.ToUpper()).Select(w=>(Guid?)w.Id).FirstOrDefaultAsync(ct);
        return foundId is Guid id?await GetWorldForRoundCheckpointAsync(id,ct):null;
    }

    public Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct = default)
        => ListWorldsAsync(forClock: false, ct);

    public Task<List<GameWorld>> ListWorldsForClockAsync(CancellationToken ct = default)
        => ListWorldsAsync(forClock: true, ct);

    public async IAsyncEnumerable<GameWorld> EnumerateWorldsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var ids = await WorldIdsAsync(false, ct);
        foreach (var id in ids)
        {
            var world = await GetWorldForRoundCheckpointAsync(id, ct);
            if (world != null && !world.RoundCheckpointNeedsSave && world.PendingWorldDataChange == null)
                yield return world;
        }
    }

    private async Task<List<GameWorld>> ListWorldsAsync(bool forClock, CancellationToken ct)
    {
        Sweep(Environment.TickCount64);
        // A cached world can be active between checkpoints. Include its ID even
        // when the last persisted state was PREPARING.
        var cached = forClock
            ? MemoryCache.Values.Select(e => e.World)
                .Where(w => w.RoundCheckpointNeedsSave || w.PendingWorldDataChange != null || IsClockState(w.RoundState))
                .Select(w => w.Id).ToList()
            : [];
        var ids = await WorldIdsAsync(forClock, ct);

        var result = new List<GameWorld>(ids.Count);
        foreach (var id in ids.Union(cached))
        {
            var world=await GetWorldForRoundCheckpointAsync(id,ct);
            if (world != null && (forClock
                ? world.RoundCheckpointNeedsSave || world.PendingWorldDataChange != null || IsClockState(world.RoundState)
                : !world.RoundCheckpointNeedsSave && world.PendingWorldDataChange == null))
                result.Add(world);
        }
        return result;
    }

    private async Task<List<Guid>> WorldIdsAsync(bool forClock, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        var query = db.Worlds.AsNoTracking();
        if (forClock)
            query = query.Where(w => w.RoundState >= (int)RoundState.RUNNING_FIRST_HALF
                && w.RoundState <= (int)RoundState.EXTRA_TIME_HALFTIME);
        return await query.OrderByDescending(w => w.UpdatedAtUtc).Select(w => w.Id).ToListAsync(ct);
    }

    private static bool IsClockState(RoundState state) => state is
        RoundState.RUNNING_FIRST_HALF or RoundState.RUNNING_SECOND_HALF or RoundState.RUNNING_EXTRA_TIME
        or RoundState.GLOBAL_PAUSED or RoundState.HALFTIME_PAUSED
        or RoundState.EXTRA_TIME_BREAK or RoundState.EXTRA_TIME_HALFTIME;

    public Task SaveWorldAsync(GameWorld world, CancellationToken ct = default)
        => SaveWorldAsync(world, ct, previousPauseVersion: null);

    public Task SavePauseTransitionAsync(GameWorld world, long previousVersion)
        => SaveWorldAsync(world, CancellationToken.None, previousVersion);

    public Task SaveRoundCheckpointAsync(GameWorld world, bool retry)
        => SaveWorldAsync(world, CancellationToken.None, previousPauseVersion: null, retryCheckpoint: retry);

    private async Task SaveWorldAsync(GameWorld world, CancellationToken ct, long? previousPauseVersion, bool retryCheckpoint = false)
    {
        var gate = SaveLocks[(uint)world.Id.GetHashCode() % SaveLocks.Length];
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

        if (!isCheckpointState) { Remember(world); return; }
        HistoryEngine.Observe(world);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        bool sqlite = db.Database.IsSqlite();
        bool postgres = db.Database.IsNpgsql();
        // A normal PostgreSQL checkpoint can prepare its payload before opening
        // the connection. Retries reconcile an ambiguous commit before encoding.
        string? json = postgres && !retryCheckpoint
            ? await SerializeSnapshotAsync(world, compress: false, ct)
            : null;

        var transactionStart = Stopwatch.GetTimestamp();
        await using var authorityTransaction = _authority != null && postgres
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        if (_authority != null) await _authority.FenceWriteAsync(db, ct);

        if (retryCheckpoint)
        {
            var persisted = await db.Worlds.AsNoTracking()
                .Where(w => w.Id == world.Id && w.RoundVersion >= world.RoundVersion)
                .Select(w => w.SnapshotJson).FirstOrDefaultAsync(ct);
            if (persisted != null)
            {
                using var document = await WorldSnapshotCodec.ReadDocumentAsync(persisted, ct);
                if (document.RootElement.TryGetProperty("roundCheckpointId", out var checkpoint)
                    && checkpoint.ValueKind == JsonValueKind.String
                    && checkpoint.GetGuid() == world.RoundCheckpointId)
                {
                    world.PersistedRoundVersion = world.RoundVersion;
                    world.RequiresDurableMatchInteractionSave = false;
                    return;
                }

                throw new DbUpdateConcurrencyException("The persisted round has changed.");
            }
        }

        // Track the actual concurrency token and scalar header without reading SnapshotJson.
        var existing = await db.Worlds.AsNoTracking().Where(w=>w.Id==world.Id).Select(w=>new WorldRecord {
            Id=w.Id,Name=w.Name,InviteCode=w.InviteCode,Seed=w.Seed,TotalClubs=w.TotalClubs,
            SeasonNumber=w.SeasonNumber,RoundNumber=w.RoundNumber,RoundState=w.RoundState,RoundVersion=w.RoundVersion,
            CurrentMatchMinute=w.CurrentMatchMinute,CreatedAtUtc=w.CreatedAtUtc,UpdatedAtUtc=w.UpdatedAtUtc
        }).FirstOrDefaultAsync(ct);
        if (previousPauseVersion is long expected && existing?.RoundVersion != expected)
            throw new DbUpdateConcurrencyException("The persisted pause has changed.");
        bool compress = false;
        if (sqlite)
        {
            if (existing == null)
                compress = _snapshotOptions?.CompressNewWorlds == true;
            else if (_snapshotFormats.TryGetValue(world, out var format))
                compress = format.Compressed;
            else
                compress = await db.Worlds.Where(w => w.Id == world.Id)
                    .Select(w => w.SnapshotJson.Substring(0, WorldSnapshotCodec.Prefix.Length) == WorldSnapshotCodec.Prefix)
                    .SingleAsync(ct);
        }
        json ??= await SerializeSnapshotAsync(world, compress, ct);
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
            var historyStart = Stopwatch.GetTimestamp();
            int oldestLedgerSeason = world.Transactions.Count == 0 ? world.SeasonNumber : world.Transactions.Min(t => t.SeasonNumber);
            var existingLedgerIds = await db.FinancialLedger
                .Where(l => l.GameWorldId == world.Id && l.SeasonNumber >= oldestLedgerSeason)
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
            _metrics?.History.Record(historyStart);
        }

        var saveStart = Stopwatch.GetTimestamp();
        await db.SaveChangesAsync(ct);
        _metrics?.Save.Record(saveStart);
        if (authorityTransaction != null)
        {
            var commitStart = Stopwatch.GetTimestamp();
            await authorityTransaction.CommitAsync(ct);
            _metrics?.Commit.Record(commitStart);
            _metrics?.Transaction.Record(transactionStart);
        }
        if (sqlite) _snapshotFormats.AddOrUpdate(world, new(compress));
        world.PersistedRoundVersion = world.RoundVersion;
        // A failed first save must never make an unpersisted world visible to confirmation retries.
        world.RequiresDurableMatchInteractionSave = false;
        Remember(world);
        }
        catch when (previousPauseVersion.HasValue)
        {
            // A failed write may have committed. The next command must read the persisted
            // snapshot and receipt before deciding whether to apply the transition.
            MemoryCache.TryRemove(world.Id, out _);
            WorldIdentity.TryRemove(world.Id, out _);
            throw;
        }
        catch
        {
            if (WorldIdentity.TryGetValue(world.Id, out var reference) &&
                reference.TryGetTarget(out var current) && ReferenceEquals(current, world))
            {
                world.RequiresDurableMatchInteractionSave = true;
                Remember(world);
            }
            throw;
        }
        finally { gate.Release(); }
    }

    private async Task<string> SerializeSnapshotAsync(GameWorld world, bool compress, CancellationToken ct)
    {
        var start = Stopwatch.GetTimestamp();
        var snapshot = await WorldSnapshotCodec.WriteAsync(world, JsonOptions, compress, ct);
        _metrics?.Serialization.Record(start, snapshot.Length);
        return snapshot;
    }
}

public sealed record WorldCacheOptions(int InactiveCount, TimeSpan InactiveLifetime);

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
