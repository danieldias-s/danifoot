using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Elifoot.Infrastructure;

/// <summary>One authoritative API process per world database while live state is kept in process.</summary>
public sealed class WorldAuthority(string provider, string connectionString) : IAsyncDisposable
{
    private const int NamespaceKey = 0x454C4946;
    private const int AuthorityKey = 98;
    private const int WritesKey = 99;
    private readonly CancellationTokenSource _stopping = new();
    private NpgsqlConnection? _session;
    private FileStream? _sqliteLock;
    private Task? _monitor;
    private int _active;
    private int _pid;
    public bool IsActive => Volatile.Read(ref _active) != 0;

    public async Task AcquireAsync(Action onLost)
    {
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
            if (!Path.IsPathFullyQualified(dataSource))
                throw new InvalidOperationException("A autoridade SQLite requer um caminho de arquivo absoluto.");
            _sqliteLock = new FileStream(dataSource + ".authority", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Volatile.Write(ref _active, 1);
            return;
        }
        if (!string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A autoridade requer SQLite em disco ou PostgreSQL.");

        var settings = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false, Multiplexing = false };
        _session = new NpgsqlConnection(settings.ConnectionString);
        try
        {
            await _session.OpenAsync();
            _pid = _session.ProcessID;
            // Drain in-flight writes from the former owner before making a new process authoritative.
            await using (var transaction = await _session.BeginTransactionAsync())
            {
                await using var command = _session.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"SELECT pg_advisory_xact_lock({NamespaceKey}, {WritesKey})";
                await command.ExecuteNonQueryAsync();
                command.CommandText = $"SELECT pg_try_advisory_lock({NamespaceKey}, {AuthorityKey})";
                if (await command.ExecuteScalarAsync() is not true)
                    throw new InvalidOperationException("Já existe um executor para este banco de mundos.");
                await transaction.CommitAsync();
            }
            Volatile.Write(ref _active, 1);
            _monitor = MonitorAsync(onLost, _stopping.Token);
        }
        catch
        {
            await _session.DisposeAsync();
            _session = null;
            throw;
        }
    }

    public async Task FenceWriteAsync(ElifootDbContext db, CancellationToken ct)
    {
        if (!IsActive) throw new IOException("O executor perdeu a autoridade sobre o banco de mundos.");
        if (_session == null) return;
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock_shared({NamespaceKey}, {WritesKey})", ct);
        bool owned = await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM pg_locks
                WHERE locktype = 'advisory' AND granted AND objsubid = 2
                  AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
                  AND classid = {NamespaceKey}::oid AND objid = {AuthorityKey}::oid AND pid = {_pid}
            ) AS "Value"
            """).SingleAsync(ct);
        if (!owned) throw new IOException("O executor perdeu a autoridade sobre o banco de mundos.");
    }

    private async Task MonitorAsync(Action onLost, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await using var command = _session!.CreateCommand();
                command.CommandText = "SELECT 1";
                command.CommandTimeout = 2;
                await command.ExecuteScalarAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch
        {
            Volatile.Write(ref _active, 0);
            try { await _session!.CloseAsync(); }
            finally { onLost(); }
        }
    }

    public void Stop() => Volatile.Write(ref _active, 0);

    public async ValueTask DisposeAsync()
    {
        Stop();
        await _stopping.CancelAsync();
        if (_monitor != null) await _monitor;
        if (_session != null) await _session.DisposeAsync();
        _sqliteLock?.Dispose();
        _stopping.Dispose();
    }
}
