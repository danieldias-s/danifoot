using Microsoft.EntityFrameworkCore;

namespace Elifoot.Infrastructure;

public sealed class AccountRecord
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string NormalizedUsername { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public string Status { get; set; } = "Active";
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}

public sealed class AccountCoachRecord
{
    public Guid CoachId { get; set; }
    public Guid AccountId { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AccountSessionRecord
{
    public string IdHash { get; set; } = "";
    public Guid AccountId { get; set; }
    public Guid CoachId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

public sealed class LegacyAccountGrantRecord
{
    public string TokenHash { get; set; } = "";
    public Guid AccountId { get; set; }
    public Guid CoachId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class AccountLinkAuditRecord
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CoachId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Outcome { get; set; } = "";
    public string Reason { get; set; } = "";
}

/// Additive, idempotent schema upgrade. Does not update the world/save tables.
public static class AccountSchema
{
    public static async Task Ensure(ElifootDbContext db, CancellationToken ct = default)
    {
        var pg = db.Database.IsNpgsql();
        var id = pg ? "uuid" : "TEXT";
        var date = pg ? "timestamp with time zone" : "TEXT";
        var boolean = pg ? "boolean" : "INTEGER";
        var statements = new[]
        {
            $"""CREATE TABLE IF NOT EXISTS "ef_accounts" ("Id" {id} PRIMARY KEY, "Username" TEXT NOT NULL, "NormalizedUsername" TEXT NOT NULL UNIQUE, "DisplayName" TEXT NOT NULL, "PasswordHash" TEXT NOT NULL, "CreatedAtUtc" {date} NOT NULL, "Status" TEXT NOT NULL, "FailedLoginCount" INTEGER NOT NULL, "LockedUntilUtc" {date} NULL)""",
            $"""CREATE TABLE IF NOT EXISTS "ef_account_coaches" ("CoachId" {id} PRIMARY KEY, "AccountId" {id} NOT NULL REFERENCES "ef_accounts" ("Id"), "DisplayName" TEXT NOT NULL, "IsPrimary" {boolean} NOT NULL, "CreatedAtUtc" {date} NOT NULL)""",
            $"""CREATE TABLE IF NOT EXISTS "ef_account_sessions" ("IdHash" TEXT PRIMARY KEY, "AccountId" {id} NOT NULL REFERENCES "ef_accounts" ("Id"), "CoachId" {id} NOT NULL REFERENCES "ef_account_coaches" ("CoachId"), "ExpiresAtUtc" {date} NOT NULL, "RevokedAtUtc" {date} NULL)""",
            $"""CREATE TABLE IF NOT EXISTS "ef_account_link_grants" ("TokenHash" TEXT PRIMARY KEY, "AccountId" {id} NOT NULL REFERENCES "ef_accounts" ("Id"), "CoachId" {id} NOT NULL, "ExpiresAtUtc" {date} NOT NULL, "ConsumedAtUtc" {date} NULL, "Reason" TEXT NOT NULL)""",
            $"""CREATE TABLE IF NOT EXISTS "ef_account_link_audit" ("Id" {id} PRIMARY KEY, "AccountId" {id} NOT NULL, "CoachId" {id} NULL, "CreatedAtUtc" {date} NOT NULL, "Outcome" TEXT NOT NULL, "Reason" TEXT NOT NULL)""",
            """CREATE INDEX IF NOT EXISTS "IX_ef_account_coaches_AccountId" ON "ef_account_coaches" ("AccountId")""",
            """CREATE INDEX IF NOT EXISTS "IX_ef_account_sessions_AccountId" ON "ef_account_sessions" ("AccountId")""",
        };
        foreach (var statement in statements) await db.Database.ExecuteSqlRawAsync(statement, ct);
    }
}
