using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

if (args.FirstOrDefault() == "sqlite-to-postgres")
{
    try
    {
        await SqliteToPostgres.RunAsync(args.Skip(1).ToArray());
    }
    catch (Exception migrationFailure)
    {
        Console.Error.WriteLine(migrationFailure.Message);
        Environment.ExitCode = 1;
    }
    return;
}
if (args.FirstOrDefault() == "compression-profile")
{
    await CompressionProfile.RunAsync(args.Skip(1).ToArray());
    return;
}
if (args.FirstOrDefault() == "stage7-career")
{
    await Stage7Career.RunAsync(args.Skip(1).ToArray());
    return;
}

if (args.Length < 3 || args[0] is not ("motor" or "seasons" or "storage-pair" or "season-fixtures"))
    throw new ArgumentException("Uso: EngineAudit motor|seasons|storage-pair|season-fixtures pasta-nova clubes [níveis, temporadas ou DBs de auditoria]");
string mode = args[0];
string output = Path.GetFullPath(args[1]);
int clubs = int.Parse(args[2]);
if (Directory.Exists(output)) throw new IOException("Use uma pasta de saída nova.");
Directory.CreateDirectory(output);
var results = new List<object>();
string report = Path.Combine(output, "results.json");
using var process = Process.GetCurrentProcess();
using var stop = new CancellationTokenSource();
using var monitor = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
var memory = new ConcurrentQueue<(long Rss, long Private)>();
var safety = Task.Run(async () =>
{
    while (await monitor.WaitForNextTickAsync())
    {
        process.Refresh();
        memory.Enqueue((process.WorkingSet64, process.PrivateMemorySize64));
        if (process.PrivateMemorySize64 > 5L * 1024 * 1024 * 1024
            || new DriveInfo(Path.GetPathRoot(output)!).AvailableFreeSpace < 12L * 1024 * 1024 * 1024)
            stop.Cancel();
    }
});
var json = new JsonSerializerOptions { WriteIndented = true };
var gameJson = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
const double MiB = 1024d * 1024d;
var baseline = process.WorkingSet64;
string? error = null;
try
{
    if (mode == "motor")
    {
        if (args.Length == 3) await EngineTier(1, publish: true);
        await EngineTier(1, publish: false);
        foreach (int count in args.Skip(3).Select(int.Parse))
        {
            await EngineTier(count, publish: true);
            if (stop.IsCancellationRequested) break;
        }
    }
    else if (mode == "seasons")
    {
        await SeasonRun(int.Parse(args[3]));
    }
    else if (mode == "storage-pair")
    {
        await CompareStorage(args.Skip(3));
    }
    else
    {
        await SeasonFixtures(args.Skip(3).Select(int.Parse).ToHashSet());
    }
}
catch (Exception failure)
{
    error = failure.ToString();
    Console.Error.WriteLine(error);
}
finally
{
    monitor.Dispose();
    await safety;
    SaveReport();
}
if (error != null) Environment.ExitCode = 1;

void SaveReport() => File.WriteAllText(report, JsonSerializer.Serialize(new { mode, clubs, baseline, results, error }, json));

static MultiplayerRoundOrchestrator Orchestrator(IWorldRepository repo) =>
    new(repo, NullMatchRoomNotificationPublisher.Instance);

async Task SeasonFixtures(HashSet<int> milestones)
{
    if (milestones.Count == 0 || milestones.Min() < 1)
        throw new ArgumentException("Informe temporadas positivas.");
    var memoryRepo = new InMemoryWorldRepository();
    var (world, coach) = await Create(memoryRepo, 0);
    var ledger = new Dictionary<Guid, FinancialTransaction>();
    var timer = Stopwatch.StartNew();
    int matches = 0;
    foreach (int season in Enumerable.Range(1, milestones.Max()))
    {
        while (world.SeasonNumber == season)
        {
            stop.Token.ThrowIfCancellationRequested();
            matches += (await Play(memoryRepo, world, coach)).Matches;
            foreach (var entry in world.Transactions) ledger.TryAdd(entry.Id, entry);
        }
        if (milestones.Contains(season))
        {
            string path = Path.Combine(output, $"season-{season}.db");
            string connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString;
            using var services = new ServiceCollection().AddDbContext<ElifootDbContext>(o => o.UseSqlite(connection))
                .AddSingleton<IWorldRepository, EfWorldRepository>().BuildServiceProvider();
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
            await db.Database.EnsureCreatedAsync(stop.Token);
            await services.GetRequiredService<IWorldRepository>().SaveWorldAsync(world, stop.Token);
            var included = world.Transactions.Select(t => t.Id).ToHashSet();
            foreach (var batch in ledger.Values.Where(t => !included.Contains(t.Id)).Chunk(2048))
            {
                db.FinancialLedger.AddRange(batch.Select(t => new FinancialLedgerRecord {
                    Id = t.Id, GameWorldId = world.Id, ClubId = t.ClubId, SeasonNumber = t.SeasonNumber,
                    RoundNumber = t.RoundNumber, Category = (int)t.Category, Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter, Description = t.Description, IdempotencyKey = t.IdempotencyKey,
                    CreatedAtUtc = t.CreatedAtUtc
                }));
                await db.SaveChangesAsync(stop.Token);
                db.ChangeTracker.Clear();
            }
            results.Add(new { season, path, ledger = ledger.Count, matches, seconds = timer.Elapsed.TotalSeconds });
            SaveReport();
        }
        Console.WriteLine($"Temporada {season}, {matches} partidas cumulativas, {timer.Elapsed.TotalSeconds:F1} s");
    }
}

async Task<(GameWorld World, Guid Coach)> Create(IWorldRepository repo, int index)
{
    var driver = Orchestrator(repo);
    var world = await driver.CreateWorldAsync(new CreateWorldRequest(
        $"Auditoria {index}", MaxHumans: 1, TotalClubs: clubs, Seed: 199801 + index), stop.Token);
    var coach = Guid.NewGuid();
    var joined = await driver.JoinWorldAndAssignClubAsync(world.Id, coach, "Auditoria", $"audit-{index}",
        joinUnemployed: true, ct: stop.Token);
    if (!joined.Success) throw new InvalidOperationException(joined.Error);
    return (world, coach);
}

async Task<(int Matches, double AdvanceMs)> Play(IWorldRepository repo, GameWorld world, Guid coach)
{
    var driver = Orchestrator(repo);
    int season = world.SeasonNumber, round = world.RoundNumber;
    var ready = await driver.SubmitUnemployedReadyAsync(
        new(world.Id, coach, $"ready-{season}-{round}", world.RoundVersion), stop.Token);
    if (!ready.RoundStarted) throw new InvalidOperationException(ready.Error ?? "Rodada não iniciou.");
    for (int tick = 0; tick < 132 && world.RoundState != RoundState.RESULTS; tick++)
    {
        var snapshot = await driver.AdvanceClockTickAsync(world.Id, stop.Token)
            ?? throw new InvalidOperationException("Mundo desapareceu.");
        if (snapshot.GameWorldId != world.Id || snapshot.PendingPenalty != null) throw new InvalidOperationException("Estado inesperado.");
        if (snapshot.ActivePauseWindow is { } pause)
        {
            var continued = await driver.ConfirmContinueMatchAsync(new(world.Id, coach,
                $"continue-{season}-{round}-{tick}") { PauseWindowId = pause.PauseWindowId }, stop.Token);
            if (!continued.Success) throw new InvalidOperationException(continued.Error);
        }
    }
    if (world.RoundState != RoundState.RESULTS) throw new InvalidOperationException("Rodada incompleta.");
    int matches = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world).Count(m => m.IsFinished);
    var timer = Stopwatch.StartNew();
    await driver.AdvanceToNextRoundPreparationAsync(world.Id, stop.Token, season, round);
    if (world.RoundState != RoundState.PREPARING ||
        !(world.SeasonNumber == season && world.RoundNumber == round + 1 ||
          world.SeasonNumber == season + 1 && world.RoundNumber == 1))
        throw new InvalidOperationException("Avanço inválido.");
    return (matches, timer.Elapsed.TotalMilliseconds);
}

async Task EngineTier(int count, bool publish)
{
    var repo = new InMemoryWorldRepository();
    var worlds = new List<(GameWorld World, Guid Coach)>();
    for (int i = 0; i < count; i++)
    {
        stop.Token.ThrowIfCancellationRequested();
        worlds.Add(await Create(repo, i));
    }
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
    long heapBefore = GC.GetTotalMemory(false);
    process.Refresh();
    long rssBefore = process.WorkingSet64;
    var cpuBefore = process.TotalProcessorTime;
    long allocatedBefore = GC.GetTotalAllocatedBytes(true);
    memory.Clear();
    var latencies = new ConcurrentBag<double>();
    var fixtures = new ConcurrentBag<int>();
    var timer = Stopwatch.StartNew();
    int concurrency = Math.Min(count, Math.Min(20, Environment.ProcessorCount));
    await Parallel.ForEachAsync(worlds, new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = stop.Token },
        async (entry, ct) =>
        {
            var roundTimer = Stopwatch.StartNew();
            var completed = await Play(repo, entry.World, entry.Coach);
            latencies.Add(roundTimer.Elapsed.TotalMilliseconds);
            fixtures.Add(completed.Matches);
        });
    timer.Stop();
    process.Refresh();
    double cpuSec = (process.TotalProcessorTime - cpuBefore).TotalSeconds;
    long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
    var samples = memory.ToArray();
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
    long heapAfter = GC.GetTotalMemory(false);
    var sorted = latencies.Order().ToArray();
    double P(double q) => sorted[(int)Math.Ceiling(q * sorted.Length) - 1];
    var result = new { count, concurrency, seconds = timer.Elapsed.TotalSeconds, cpuSec, cpuSecPerRound = cpuSec / count,
        completed = sorted.Length, roundsSec = count / timer.Elapsed.TotalSeconds, p50 = P(.50), p95 = P(.95), p99 = P(.99),
        minMatches = fixtures.Min(), maxMatches = fixtures.Max(), allocatedMiBPerRound = allocated / MiB / count,
        rssBeforeMiB = rssBefore / MiB, rssPeakMiB = samples.Select(s => s.Rss).Append(rssBefore).Max() / MiB,
        heapBeforeMiB = heapBefore / MiB, heapAfterMiB = heapAfter / MiB };
    Console.WriteLine(JsonSerializer.Serialize(result));
    if (publish)
    {
        results.Add(result);
        SaveReport();
        if (timer.Elapsed.TotalSeconds > 180 || P(.95) > 25000) stop.Cancel();
    }
    GC.KeepAlive(repo);
}

async Task SeasonRun(int seasons)
{
    string dbPath = Path.Combine(output, "season.db");
    var builder = new SqliteConnectionStringBuilder { DataSource = dbPath };
    using var services = new ServiceCollection().AddDbContext<ElifootDbContext>(o => o.UseSqlite(builder.ConnectionString))
        .AddSingleton<IWorldRepository, EfWorldRepository>().BuildServiceProvider();
    using (var scope = services.CreateScope())
        await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync(stop.Token);
    var persist = services.GetRequiredService<IWorldRepository>();
    var repo = new InMemoryWorldRepository();
    var (world, coach) = await Create(repo, 0);
    await persist.SaveWorldAsync(world, stop.Token);
    await Measure(0, 0, 0, 0);
    for (int season = 1; season <= seasons; season++)
    {
        var seasonTimer = Stopwatch.StartNew();
        int matches = 0;
        while (world.SeasonNumber == season)
        {
            stop.Token.ThrowIfCancellationRequested();
            var played = await Play(repo, world, coach);
            matches += played.Matches;
            var saveTimer = Stopwatch.StartNew();
            await persist.SaveWorldAsync(world, stop.Token);
            if (world.SeasonNumber != season)
                await Measure(season, seasonTimer.Elapsed.TotalMilliseconds, played.AdvanceMs + saveTimer.Elapsed.TotalMilliseconds, matches);
        }
    }

    async Task Measure(int season, double seasonMs, double finalAdvanceAndSaveMs, int matches)
    {
        await using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(stop.Token);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT SeasonNumber,count(*) FROM ef_financial_ledger GROUP BY 1 ORDER BY 1";
        var ledger = new Dictionary<int, int>();
        using (var reader = command.ExecuteReader())
            while (reader.Read()) ledger.Add(reader.GetInt32(0), reader.GetInt32(1));
        command.CommandText = "SELECT name,sum(pgsize) FROM dbstat GROUP BY name";
        var objects = new Dictionary<string, long>();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read()) objects.Add(reader.GetString(0), reader.GetInt64(1));
        }
        catch (SqliteException) { /* The deployed SQLite library may omit dbstat. */ }
        command.CommandText = "PRAGMA page_count";
        long pages = (long)command.ExecuteScalar()!;
        command.CommandText = "PRAGMA freelist_count";
        long freePages = (long)command.ExecuteScalar()!;
        command.CommandText = "PRAGMA page_size";
        long pageSize = (long)command.ExecuteScalar()!;
        command.CommandText = "SELECT length(CAST(SnapshotJson AS blob)) FROM ef_worlds";
        long snapshotBytes = (long)command.ExecuteScalar()!;
        var components = JsonSerializer.SerializeToElement(world, gameJson).EnumerateObject()
            .Where(p => p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            .ToDictionary(p => p.Name, p => new { bytes = System.Text.Encoding.UTF8.GetByteCount(p.Value.GetRawText()),
                count = p.Value.ValueKind == JsonValueKind.Array ? p.Value.GetArrayLength() : p.Value.EnumerateObject().Count() });
        long Size(string suffix) => File.Exists(dbPath + suffix) ? new FileInfo(dbPath + suffix).Length : 0;
        var result = new { season, seasonMs, finalAdvanceAndSaveMs, matches, ledger, objects, snapshotBytes,
            liveBytes = (pages - freePages) * pageSize, allocatedBytes = pages * pageSize, freePages,
            dbBytes = Size(""), walBytes = Size("-wal"), shmBytes = Size("-shm"), components };
        results.Add(result);
        Console.WriteLine(JsonSerializer.Serialize(new { season, seasonMs, finalAdvanceAndSaveMs, matches, snapshotBytes, result.liveBytes, result.walBytes }));
        SaveReport();
    }
}

async Task CompareStorage(IEnumerable<string> fixturePaths)
{
    var memoryRepo = new InMemoryWorldRepository();
    var (initial, _) = await Create(memoryRepo, 0);
    await Compare(initial, 0, null);
    foreach (var fixture in fixturePaths)
    {
        var source = Path.GetFullPath(fixture);
        if (!File.Exists(source) || File.Exists(source + "-wal") || File.Exists(source + "-shm"))
            throw new IOException("Use um banco descartável de auditoria, fechado e sem WAL/SHM.");
        string copy = Path.Combine(output, $"input-{Guid.NewGuid():N}.db");
        File.Copy(source, copy);
        using var services = Store(Connection(copy), compress: false);
        var worldId = Guid.Parse((string)(await Scalar(Connection(copy), "SELECT Id FROM ef_worlds"))!);
        var world = (await services.GetRequiredService<IWorldRepository>().GetWorldAsync(worldId, stop.Token))!;
        await Compare(world, world.SeasonNumber - 1, copy);
    }

    async Task Compare(GameWorld world, int season, string? ledgerSource)
    {
        var normal = await RunVariant(world, season, false, ledgerSource);
        var packed = await RunVariant(world, season, true, ledgerSource);
        if (normal != packed)
            throw new InvalidDataException($"O histórico divergiu na temporada {season}.");
        Console.WriteLine($"Temporada {season}: SHA-256 do JSON equivalente {normal}");
    }

    async Task<(string Persisted, string NextRound)> RunVariant(GameWorld world, int season,
        bool compressed, string? ledgerSource)
    {
        string path = Path.Combine(output, $"s{season}-{(compressed ? "gzip" : "json")}.db");
        var connection = Connection(path);
        using var services = Store(connection, compressed);
        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync(stop.Token);
        var repo = services.GetRequiredService<IWorldRepository>();
        await using var anchor = new SqliteConnection(connection);
        await anchor.OpenAsync(stop.Token);
        using (var wal = anchor.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode=WAL";
            if (!string.Equals((string?)await wal.ExecuteScalarAsync(stop.Token), "wal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("O SQLite de auditoria não entrou no modo WAL.");
        }
        await repo.SaveWorldAsync(world, stop.Token);
        if (ledgerSource != null)
        {
            using var merge = anchor.CreateCommand();
            merge.CommandText = "ATTACH DATABASE $source AS fixture";
            merge.Parameters.AddWithValue("$source", ledgerSource);
            await merge.ExecuteNonQueryAsync(stop.Token);
            merge.CommandText = "INSERT OR IGNORE INTO ef_financial_ledger SELECT * FROM fixture.ef_financial_ledger";
            await merge.ExecuteNonQueryAsync(stop.Token);
            merge.CommandText = "SELECT count(*) FROM (SELECT * FROM fixture.ef_financial_ledger EXCEPT SELECT * FROM ef_financial_ledger)";
            if (Convert.ToInt64(await merge.ExecuteScalarAsync(stop.Token)) != 0)
                throw new InvalidDataException("O ledger de auditoria divergiu do original.");
            merge.CommandText = "DETACH DATABASE fixture";
            await merge.ExecuteNonQueryAsync(stop.Token);
        }

        var saves = new List<object>();
        var loads = new List<object>();
        var walSamples = new List<long>();
        for (int sample = 0; sample < 3; sample++)
        {
            await Timed(saves, () => repo.SaveWorldAsync(world, stop.Token));
            walSamples.Add(FileBytes(path + "-wal"));
            EfWorldRepository.ClearCacheForTesting();
            await Timed(loads, async () => {
                var loaded = await repo.GetWorldAsync(world.Id, stop.Token);
                if (loaded == null || loaded.RoundVersion != world.RoundVersion)
                    throw new InvalidDataException("O mundo não foi recuperado do banco.");
            });
        }

        long pageCount = Convert.ToInt64(await Scalar(connection, "PRAGMA page_count"));
        long freePages = Convert.ToInt64(await Scalar(connection, "PRAGMA freelist_count"));
        long pageSize = Convert.ToInt64(await Scalar(connection, "PRAGMA page_size"));
        long live = (pageCount - freePages) * pageSize;
        var snapshotBytes = Convert.ToInt64(await Scalar(connection, "SELECT length(CAST(SnapshotJson AS BLOB)) FROM ef_worlds"));
        long physicalBytes = FileBytes(path), walBytes = FileBytes(path + "-wal"), shmBytes = FileBytes(path + "-shm");
        var result = await Scalar(connection, "PRAGMA quick_check");
        if (!Equals(result, "ok")) throw new InvalidDataException($"SQLite quick_check: {result}");
        string hash = await ReadHash(connection);
        var ledgerCount = await Scalar(connection, "SELECT count(*) FROM ef_financial_ledger");
        var historyCount = await Scalar(connection, "SELECT count(*) FROM ef_season_histories");
        await Scalar(connection, "PRAGMA wal_checkpoint(TRUNCATE)");
        var walAfterCheckpoint = FileBytes(path + "-wal");
        var physicalAfterCheckpoint = FileBytes(path);
        // Preserve the measurement DB and create a separate compacted copy.
        var vacuumCopy = Path.ChangeExtension(path, ".vacuum.db");
        await Scalar(connection, $"VACUUM INTO '{vacuumCopy.Replace("'", "''")}'");
        var allocatedAfterVacuum = FileBytes(vacuumCopy);

        var restart = (await repo.GetWorldAsync(world.Id, stop.Token))!;
        var round = new List<object>();
        var playing = new InMemoryWorldRepository();
        await playing.SaveWorldAsync(restart, stop.Token);
        var nextSeason = restart.SeasonNumber;
        var nextRound = restart.RoundNumber;
        await Timed(round, async () => {
            await Play(playing, restart, restart.HumanCoaches.Single().Id);
            await repo.SaveWorldAsync(restart, stop.Token);
        });
        var originalPlayerIds = world.Players.Select(p => p.Id).ToHashSet();
        var outcome = new {
            matches = restart.Fixtures.Where(m => m.SeasonNumber == nextSeason && m.RoundNumber == nextRound)
                .Select(m => new { m.Id, m.HomeGoals, m.AwayGoals, m.HomePenaltyGoals, m.AwayPenaltyGoals,
                    events = m.Events.Select(e => new { e.Minute, e.EventType, e.PrimaryPlayerId, e.AssistPlayerId }) }),
            players = restart.Players.Where(p => originalPlayerIds.Contains(p.Id))
                .Select(p => new { p.Id, p.ClubId, p.Strength, p.InjuryRoundsRemaining, p.InjuryDaysRemaining,
                    p.SeasonGoals, p.SeasonYellows, p.SeasonReds, p.SeasonMatches }),
            economy = restart.Transactions.Where(t => t.SeasonNumber == nextSeason && t.RoundNumber == nextRound)
                .Select(t => new { t.ClubId, t.Category, t.Amount, t.BalanceAfter })
        };
        string outcomeHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(outcome)));
        results.Add(new { season, compressed, snapshotBytes, live, physicalBytes, walBytes, shmBytes,
            walAfterCheckpoint, physicalAfterCheckpoint, allocatedAfterVacuum, pageCount, freePages, pageSize,
            ledgerCount, historyCount, hash, outcomeHash, saves, loads,
            round, walSamples = walSamples.ToArray() });
        SaveReport();
        return (hash, outcomeHash);
    }

    async Task Timed(List<object> samples, Func<Task> action)
    {
        stop.Token.ThrowIfCancellationRequested();
        while (memory.TryDequeue(out _)) { }
        process.Refresh();
        var cpu = process.TotalProcessorTime;
        var initialRss = process.WorkingSet64;
        var timer = Stopwatch.StartNew();
        await action();
        timer.Stop();
        process.Refresh();
        var peakRss = Math.Max(initialRss, process.WorkingSet64);
        var peakPrivate = process.PrivateMemorySize64;
        while (memory.TryDequeue(out var observation))
        {
            peakRss = Math.Max(peakRss, observation.Rss);
            peakPrivate = Math.Max(peakPrivate, observation.Private);
        }
        samples.Add(new { ms = timer.Elapsed.TotalMilliseconds, cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds,
            initialRss, peakRss, peakPrivate });
    }

    async Task<string> ReadHash(string connection)
    {
        var snapshot = (string)(await Scalar(connection, "SELECT SnapshotJson FROM ef_worlds"))!;
        const string prefix = "ELIFOOT:GZIP:1:";
        using var decoded = snapshot.StartsWith(prefix, StringComparison.Ordinal)
            ? new GZipStream(new MemoryStream(Convert.FromBase64String(snapshot[prefix.Length..])), CompressionMode.Decompress)
            : null;
        using var doc = decoded == null ? JsonDocument.Parse(snapshot) : await JsonDocument.ParseAsync(decoded, cancellationToken: stop.Token);
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            // The repository stamps each write with the wall clock.
            if (property.Name == "updatedAtUtc") continue;
            digest.AppendData(Encoding.UTF8.GetBytes(property.Name));
            digest.AppendData(Encoding.UTF8.GetBytes(property.Value.GetRawText()));
        }
        return Convert.ToHexString(digest.GetHashAndReset());
    }

    async Task<object?> Scalar(string connection, string sql)
    {
        await using var db = new SqliteConnection(connection);
        await db.OpenAsync(stop.Token);
        using var command = db.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(stop.Token);
    }

    static string Connection(string path) => new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString;
    static long FileBytes(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;
    static ServiceProvider Store(string connection, bool compress) => new ServiceCollection()
        .AddDbContext<ElifootDbContext>(o => o.UseSqlite(connection))
        .AddSingleton(new SqliteSnapshotOptions(compress))
        .AddSingleton<IWorldRepository, EfWorldRepository>().BuildServiceProvider();
}
