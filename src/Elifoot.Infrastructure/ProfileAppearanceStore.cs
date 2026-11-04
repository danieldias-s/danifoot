using Microsoft.EntityFrameworkCore;

namespace Elifoot.Infrastructure;

/// Trainer-level presentation metadata, outside the world/save aggregate.
public static class ProfileAppearanceStore
{
    public const string Current = "CURRENT";
    public const string Classic98 = "CLASSIC_98";

    public static Task EnsureSchema(ElifootDbContext db) =>
        db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ef_profile_appearance" (
                "OwnerId" TEXT NOT NULL PRIMARY KEY,
                "Mode" TEXT NOT NULL CHECK ("Mode" IN ('CURRENT', 'CLASSIC_98'))
            )
            """);

    public static async Task<string> Get(ElifootDbContext db, Guid owner, CancellationToken ct) =>
        await db.Database.SqlQuery<string>($"""
            SELECT "Mode" AS "Value" FROM "ef_profile_appearance" WHERE "OwnerId" = {owner.ToString("N")}
            """).SingleOrDefaultAsync(ct) ?? Current;

    public static Task Save(ElifootDbContext db, Guid owner, string mode, CancellationToken ct)
    {
        if (owner == Guid.Empty || mode is not (Current or Classic98))
            throw new ArgumentException("Versão visual inválida.");
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ef_profile_appearance" ("OwnerId", "Mode") VALUES ({owner.ToString("N")}, {mode})
            ON CONFLICT ("OwnerId") DO UPDATE SET "Mode" = excluded."Mode"
            """, ct);
    }
}
