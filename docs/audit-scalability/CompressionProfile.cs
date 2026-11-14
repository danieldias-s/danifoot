using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Domain;
using Microsoft.Data.Sqlite;

internal static class CompressionProfile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private record Sample<T>(T Value, double Ms, double CpuMs, long AllocatedBytes);
    private record Format(string Name, Func<Stream, Stream> Compress, Func<Stream, Stream> Decompress);

    internal static async Task RunAsync(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Uso: compression-profile pasta-nova <DBs descartáveis com JSON>.");
        var target = Path.GetFullPath(args[0]);
        if (Directory.Exists(target)) throw new IOException("O diretório de destino já existe.");
        Directory.CreateDirectory(target);
        using var process = Process.GetCurrentProcess();
        var codecs = new[] {
            new Format("gzip-fastest", s => new GZipStream(s, CompressionLevel.Fastest, true), s => new GZipStream(s, CompressionMode.Decompress)),
            new Format("gzip-optimal", s => new GZipStream(s, CompressionLevel.Optimal, true), s => new GZipStream(s, CompressionMode.Decompress)),
            new Format("brotli-fastest", s => new BrotliStream(s, CompressionLevel.Fastest, true), s => new BrotliStream(s, CompressionMode.Decompress)),
            new Format("brotli-optimal", s => new BrotliStream(s, CompressionLevel.Optimal, true), s => new BrotliStream(s, CompressionMode.Decompress))
        };
        var results = new List<object>();
        foreach (var file in args.Skip(1))
        {
            var snapshot = Path.Combine(target, $"input-{Guid.NewGuid():N}.db");
            var sourcePath = Path.GetFullPath(file);
            if (new[] { "-wal", "-shm", "-journal" }.Any(suffix => File.Exists(sourcePath + suffix)))
                throw new IOException("Feche o banco descartável de auditoria antes de usá-lo como fixture.");
            File.Copy(sourcePath, snapshot);
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshot, Pooling = false }.ConnectionString);
            source.Open();
            using var sourceCommand = source.CreateCommand();
            sourceCommand.CommandText = "SELECT SnapshotJson FROM ef_worlds";
            var original = (string)sourceCommand.ExecuteScalar()!;
            var world = JsonSerializer.Deserialize<GameWorld>(original, Options)!;
            int season = world.SeasonNumber - 1;
            var ledgerRows = world.Transactions.Take(4096).ToArray();
            var serialization = Measure(() => JsonSerializer.SerializeToUtf8Bytes(world, Options));
            original = null!;
            world = null!;
            var bytes = serialization.Value;
            string digest = Convert.ToHexString(SHA256.HashData(bytes));
            var formats = new List<object>();
            await using var db = new SqliteConnection($"Data Source={Path.Combine(target, $"io-{season}.db")};Pooling=False");
            db.Open();
            Execute("PRAGMA journal_mode=WAL");
            Execute("PRAGMA wal_autocheckpoint=0");
            Execute("PRAGMA synchronous=FULL");
            Execute("""
                CREATE TABLE snapshot(id INTEGER PRIMARY KEY, payload TEXT NOT NULL);
                INSERT INTO snapshot VALUES(1, '');
                CREATE TABLE idx_none(id TEXT, club TEXT, season INTEGER, round INTEGER, key TEXT);
                CREATE TABLE idx_on(id TEXT PRIMARY KEY, club TEXT, season INTEGER, round INTEGER, key TEXT, UNIQUE(club,key));
                CREATE INDEX lookup ON idx_on(season,round);
                """);
            int ledgerSamples = ledgerRows.Length;
            // Both statements have identical input and run outside the snapshot timing.
            var noIndex = Ledger("idx_none");
            var indexed = Ledger("idx_on");
            var fixture = new { season, digest, jsonBytes = bytes.Length, serialization = Timing(serialization), ledgerSamples,
                unpackIncludesSha256 = true, hydrateIncludesUnpack = true,
                ledgerWithoutIndexes = noIndex, ledgerWithIndexes = indexed, formats };
            results.Add(fixture);
            int repetitions = bytes.Length > 256 * 1024 * 1024 ? 1 : 3;
            for (int repeat = 0; repeat < repetitions; repeat++)
            {
                var plain = Measure(() => Encoding.UTF8.GetString(bytes));
                Record("json", plain.Value, null, Timing(plain));
                // Alternate execution order between repetitions.
                var ordered = repeat % 2 == 0 ? codecs.AsEnumerable() : codecs.Reverse();
                foreach (var format in ordered)
                {
                    var compressed = Measure(() =>
                    {
                        using var output = new MemoryStream();
                        using (var stream = format.Compress(output)) stream.Write(bytes);
                        return output.ToArray();
                    });
                    var envelope = Measure(() => Convert.ToBase64String(compressed.Value));
                    Record(format.Name, envelope.Value, format, Timing(compressed), Timing(envelope));
                }
            }
            Console.WriteLine($"Perfil concluído · {season} temporadas");

            object Ledger(string table)
            {
                using var transaction = db.BeginTransaction();
                using var command = db.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"INSERT INTO {table} VALUES ($id,$club,$season,$round,$key)";
                var id = command.Parameters.Add("$id", SqliteType.Text);
                var club = command.Parameters.Add("$club", SqliteType.Text);
                var s = command.Parameters.Add("$season", SqliteType.Integer);
                var round = command.Parameters.Add("$round", SqliteType.Integer);
                var key = command.Parameters.Add("$key", SqliteType.Text);
                command.Prepare();
                var write = Measure(() =>
                {
                    foreach (var item in ledgerRows)
                    {
                        id.Value = item.Id;
                        club.Value = item.ClubId;
                        s.Value = item.SeasonNumber;
                        round.Value = item.RoundNumber;
                        key.Value = item.IdempotencyKey;
                        command.ExecuteNonQuery();
                    }
                    return true;
                });
                var commit = Measure(() => { transaction.Commit(); return true; });
                return new { statement = Timing(write), commit = Timing(commit) };
            }

            void Record(string name, string value, Format? codec, object? encode, object? base64 = null)
            {
                process.Refresh();
                if (process.PrivateMemorySize64 > 5L * 1024 * 1024 * 1024)
                    throw new InvalidOperationException("Limite de memória do perfil atingido.");
                Execute("PRAGMA wal_checkpoint(TRUNCATE)");
                var begin = Measure(() => db.BeginTransaction());
                using var transaction = begin.Value;
                using var update = db.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = "UPDATE snapshot SET payload=$value WHERE id=1";
                update.Parameters.AddWithValue("$value", value);
                var write = Measure(update.ExecuteNonQuery);
                var commit = Measure(() => { transaction.Commit(); return true; });
                var read = Measure(() => (string)Execute("SELECT payload FROM snapshot")!);
                var unpack = Measure(() =>
                {
                    if (codec == null) return SHA256.HashData(Encoding.UTF8.GetBytes(read.Value));
                    using var stream = Open();
                    return SHA256.HashData(stream);
                });
                if (Convert.ToHexString(unpack.Value) != digest)
                    throw new InvalidDataException("O formato alterou o conteúdo do JSON.");
                var deserialize = Measure(() =>
                {
                    if (codec == null) return JsonSerializer.Deserialize<GameWorld>(read.Value, Options)!.RoundVersion;
                    using var stream = Open();
                    return JsonSerializer.Deserialize<GameWorld>(stream, Options)!.RoundVersion;
                });
                formats.Add(new { name, bytes = Encoding.UTF8.GetByteCount(value), encode, base64,
                    begin = Timing(begin), write = Timing(write), commit = Timing(commit), read = Timing(read),
                    unpack = Timing(unpack), hydrate = Timing(deserialize) });
                File.WriteAllText(Path.Combine(target, "profile.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
                Stream Open() => codec!.Decompress(new MemoryStream(Convert.FromBase64String(read.Value)));
            }

            object? Execute(string sql)
            {
                using var command = db.CreateCommand();
                command.CommandText = sql;
                return command.ExecuteScalar();
            }
        }

        Sample<T> Measure<T>(Func<T> action)
        {
            var cpu = process.TotalProcessorTime;
            long allocations = GC.GetTotalAllocatedBytes(true);
            var start = Stopwatch.GetTimestamp();
            T value = action();
            return new(value, Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                (process.TotalProcessorTime - cpu).TotalMilliseconds, GC.GetTotalAllocatedBytes(true) - allocations);
        }
    }

    private static object Timing<T>(Sample<T> sample) => new { ms = sample.Ms, cpuMs = sample.CpuMs, allocatedBytes = sample.AllocatedBytes };
}
