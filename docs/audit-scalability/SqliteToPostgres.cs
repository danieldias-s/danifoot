using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static class SqliteToPostgres
{
    internal static async Task RunAsync(string[] args)
    {
        if (args.Length != 2 || args[1].Length != 64)
            throw new ArgumentException("Uso: sqlite-to-postgres copia-fechada.db sha256, com STAGE4_PG.");
        string sourcePath = Path.GetFullPath(args[0]);
        if (new[] { "-wal", "-shm", "-journal" }.Any(suffix => File.Exists(sourcePath + suffix)))
            throw new IOException("Use uma cópia SQLite consistente, fechada e sem arquivos auxiliares.");
        using var sourceStream = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var sourceHash = Convert.ToHexString(await SHA256.HashDataAsync(sourceStream));
        if (!sourceHash.Equals(args[1], StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SHA-256 da cópia SQLite diverge do esperado.");

        var pg = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STAGE4_PG")
            ?? throw new ArgumentException("Configure STAGE4_PG com um PostgreSQL descartável."));
        if (pg.Host != "127.0.0.1" || pg.Database?.StartsWith("audit_", StringComparison.Ordinal) != true
            || pg.Port == 5432)
            throw new ArgumentException("Use um banco audit_* novo em loopback e porta dedicada.");
        var sqlite = new SqliteConnectionStringBuilder {
            DataSource = new Uri(sourcePath).AbsoluteUri + "?immutable=1",
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        await using var source = new ElifootDbContext(new DbContextOptionsBuilder<ElifootDbContext>()
            .UseSqlite(sqlite.ConnectionString).Options);
        await using var destination = new ElifootDbContext(new DbContextOptionsBuilder<ElifootDbContext>()
            .UseNpgsql(pg.ConnectionString).Options);
        await source.Database.OpenConnectionAsync();
        var tables = await source.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type='table'").ToListAsync();
        var integrity = await source.Database.SqlQueryRaw<string>("PRAGMA integrity_check").ToListAsync();
        if (integrity.Count != 1 || integrity[0] != "ok") throw new InvalidDataException("SQLite integrity_check falhou.");
        if ((await source.Database.SqlQueryRaw<ForeignKeyViolation>("PRAGMA foreign_key_check").ToListAsync()).Count != 0)
            throw new InvalidDataException("SQLite foreign_key_check encontrou referências quebradas.");
        string[] core = ["ef_worlds", "ef_club_summaries", "ef_financial_ledger", "ef_season_histories"];
        string[] accounts = ["ef_accounts", "ef_account_coaches", "ef_account_sessions", "ef_account_link_grants", "ef_account_link_audit"];
        const string appearanceTable = "ef_profile_appearance";
        if (!core.All(tables.Contains) || tables.Except(core.Concat(accounts).Append(appearanceTable))
            .Any(t => !t.StartsWith("sqlite_", StringComparison.Ordinal)))
            throw new InvalidDataException("Esquema SQLite fora do contrato do banco de mundos.");
        var accountsPresent = accounts.All(tables.Contains);
        if (!accountsPresent && accounts.Any(tables.Contains))
            throw new InvalidDataException("Esquema de contas incompleto.");

        bool authorityLost = false;
        await using var authority = new WorldAuthority("PostgreSQL", pg.ConnectionString);
        await authority.AcquireAsync(() => Volatile.Write(ref authorityLost, true));
        if (await destination.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='public') AS "Value"
            """).SingleAsync())
            throw new InvalidOperationException("Use um banco de destino novo, sem tabelas.");
        await destination.Database.EnsureCreatedAsync();
        await using var transaction = await destination.Database.BeginTransactionAsync();
        await authority.FenceWriteAsync(destination, CancellationToken.None);
        await ProfileAppearanceStore.EnsureSchema(destination);
        var counts = new Dictionary<string, int>();
        var snapshots = new List<object>();
        var worldIds = new HashSet<Guid>();
        var coachAccounts = new Dictionary<Guid, Guid>();
        var checks = new List<Func<Task>>();
        int truncatedTimestamps = 0;

        await Copy(source.Accounts.OrderBy(x => x.Id), destination.Accounts, accountsPresent);
        await Copy(source.AccountCoaches.OrderBy(x => x.CoachId), destination.AccountCoaches, accountsPresent, row =>
            coachAccounts.Add(row.CoachId, row.AccountId));
        await Copy(source.AccountSessions.OrderBy(x => x.IdHash), destination.AccountSessions, accountsPresent, row =>
            RequireAccountCoach(row.AccountId, row.CoachId));
        // Grants can refer to an unclaimed legacy coach, before a profile exists.
        await Copy(source.AccountLinkGrants.OrderBy(x => x.TokenHash), destination.AccountLinkGrants, accountsPresent);
        await Copy(source.AccountLinkAudit.OrderBy(x => x.Id), destination.AccountLinkAudit, accountsPresent);
        await Copy(source.Worlds.OrderBy(x => x.Id), destination.Worlds, validate: row =>
        {
            const string marker = "ELIFOOT:GZIP:1:";
            var compressed = row.SnapshotJson.StartsWith(marker, StringComparison.Ordinal);
            if (compressed)
            {
                using var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(row.SnapshotJson[marker.Length..])), CompressionMode.Decompress);
                using var json = new StreamReader(gzip, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
                row.SnapshotJson = json.ReadToEnd();
            }
            using var doc = JsonDocument.Parse(row.SnapshotJson);
            var root = doc.RootElement;
            if (root.GetProperty("id").GetGuid() != row.Id ||
                root.GetProperty("roundVersion").GetInt64() != row.RoundVersion ||
                root.GetProperty("seasonNumber").GetInt32() != row.SeasonNumber ||
                root.GetProperty("roundNumber").GetInt32() != row.RoundNumber ||
                root.GetProperty("inviteCode").GetString() != row.InviteCode)
                throw new InvalidDataException("Cabeçalho do mundo inconsistente com o snapshot.");
            worldIds.Add(row.Id);
            snapshots.Add(new { row.Id, row.RoundVersion, compressed, jsonBytes = Encoding.UTF8.GetByteCount(row.SnapshotJson),
                sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.SnapshotJson))) });
        }, batchSize: 1);
        await Copy(source.ClubSummaries.OrderBy(x => x.GameWorldId).ThenBy(x => x.Id), destination.ClubSummaries,
            validate: x => RequireWorld(x.GameWorldId));
        await Copy(source.FinancialLedger.OrderBy(x => x.Id), destination.FinancialLedger,
            validate: x => RequireWorld(x.GameWorldId));
        await Copy(source.SeasonHistories.OrderBy(x => x.Id), destination.SeasonHistories,
            validate: x => RequireWorld(x.GameWorldId));
        int appearances = 0;
        if (tables.Contains(appearanceTable))
        {
            var rows = source.Database.SqlQueryRaw<AppearanceRow>("""SELECT "OwnerId", "Mode" FROM ef_profile_appearance""")
                .AsAsyncEnumerable();
            await foreach (var row in rows)
            {
                var owner = Guid.ParseExact(row.OwnerId, "N");
                await ProfileAppearanceStore.Save(destination, owner, row.Mode, CancellationToken.None);
                if (await ProfileAppearanceStore.Get(destination, owner, CancellationToken.None) != row.Mode)
                    throw new InvalidDataException("Divergência nas preferências de aparência.");
                appearances++;
            }
        }
        counts.Add(appearanceTable, appearances);
        foreach (var verify in checks) await verify();
        sourceStream.Position = 0;
        if (Convert.ToHexString(await SHA256.HashDataAsync(sourceStream)) != sourceHash)
            throw new IOException("A cópia de origem mudou durante a importação.");
        if (Volatile.Read(ref authorityLost)) throw new IOException("Autoridade perdida durante a cópia.");
        await transaction.CommitAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { sourceHash, accountsPresent, counts, snapshots, truncatedTimestamps, verified = true },
            new JsonSerializerOptions { WriteIndented = true }));

        void RequireWorld(Guid id)
        {
            if (!worldIds.Contains(id)) throw new InvalidDataException("Registro relacional sem mundo.");
        }

        void RequireAccountCoach(Guid account, Guid coach)
        {
            if (coachAccounts.GetValueOrDefault(coach) != account)
                throw new InvalidDataException("Sessão vinculada a treinador de outra conta.");
        }

        async Task Copy<T>(IQueryable<T> query, DbSet<T> target, bool present = true,
            Action<T>? validate = null, int batchSize = 256) where T : class
        {
            var entity = destination.Model.FindEntityType(typeof(T))!;
            string table = entity.GetTableName()!;
            var keys = entity.FindPrimaryKey()!.Properties.Select(p => p.PropertyInfo!).ToArray();
            var dates = entity.GetProperties().Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
                .Select(p => p.PropertyInfo!).ToArray();
            if (await target.AnyAsync()) throw new InvalidOperationException($"Destino contém dados em {table}.");
            var hashes = new Dictionary<string, byte[]>();
            if (present)
            {
                var batch = new List<T>(batchSize);
                await foreach (var row in query.AsNoTracking().AsAsyncEnumerable())
                {
                    validate?.Invoke(row);
                    foreach (var property in dates)
                    {
                        if (property.GetValue(row) is not DateTime date) continue;
                        long ticks = date.Ticks - date.Ticks % 10;
                        if (ticks != date.Ticks) truncatedTimestamps++;
                        property.SetValue(row, new DateTime(ticks, DateTimeKind.Utc));
                    }
                    hashes.Add(Key(row), Digest(row));
                    batch.Add(row);
                    if (batch.Count == batchSize) await Flush(batch);
                }
                if (batch.Count > 0) await Flush(batch);
            }
            counts.Add(table, hashes.Count);
            checks.Add(async () =>
            {
                await foreach (var row in target.AsNoTracking().AsAsyncEnumerable())
                {
                    if (!hashes.Remove(Key(row), out var expected) || !expected.AsSpan().SequenceEqual(Digest(row)))
                        throw new InvalidDataException($"Divergência ao verificar {table}.");
                }
                if (hashes.Count != 0) throw new InvalidDataException($"Linhas ausentes em {table}.");
            });
            string Key(T row) => JsonSerializer.Serialize(keys.Select(key => key.GetValue(row)));
            byte[] Digest(T row)
            {
                if (row is not WorldRecord world) return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(row));
                // Avoid serializing the full JSON into a second outer JSON string.
                return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
                    world.Id, world.Name, world.InviteCode, world.Seed, world.TotalClubs, world.SeasonNumber,
                    world.RoundNumber, world.RoundState, world.RoundVersion, world.CurrentMatchMinute,
                    world.CreatedAtUtc, world.UpdatedAtUtc,
                    JsonHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(world.SnapshotJson)))
                }));
            }
            async Task Flush(List<T> batch)
            {
                target.AddRange(batch);
                await destination.SaveChangesAsync();
                destination.ChangeTracker.Clear();
                batch.Clear();
            }
        }
    }

    private sealed class ForeignKeyViolation
    {
        public string table { get; set; } = "";
        public long? rowid { get; set; }
        public string parent { get; set; } = "";
        public int fkid { get; set; }
    }

    private sealed class AppearanceRow
    {
        public string OwnerId { get; set; } = "";
        public string Mode { get; set; } = "";
    }
}
