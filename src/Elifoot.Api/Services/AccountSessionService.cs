using System.Security.Cryptography;
using System.Text;
using Elifoot.Application;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Elifoot.Api.Services;

public sealed class AccountSessionService(ElifootDbContext db, IPasswordHasher<AccountRecord> hasher)
{
    private static readonly AccountRecord Dummy = new() { Id = Guid.NewGuid() };
    private static readonly string DummyHash = new PasswordHasher<AccountRecord>().HashPassword(Dummy, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    public static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public async Task<AccountRecord?> Register(string username, string displayName, string password, CancellationToken ct)
    {
        var normalized = Normalize(username);
        if (await db.Accounts.AnyAsync(x => x.NormalizedUsername == normalized, ct)) return null;
        var account = new AccountRecord { Id = Guid.NewGuid(), Username = username.Trim(), NormalizedUsername = normalized,
            DisplayName = displayName.Trim(), CreatedAtUtc = DateTime.UtcNow };
        account.PasswordHash = hasher.HashPassword(account, password);
        db.Accounts.Add(account);
        db.AccountCoaches.Add(new() { CoachId = Guid.NewGuid(), AccountId = account.Id, DisplayName = account.DisplayName,
            IsPrimary = true, CreatedAtUtc = DateTime.UtcNow });
        try { await db.SaveChangesAsync(ct); return account; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return null; }
    }

    public async Task<AccountRecord?> Login(string username, string password, CancellationToken ct)
    {
        var normalized = Normalize(username);
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.NormalizedUsername == normalized, ct);
        var valid = hasher.VerifyHashedPassword(account ?? Dummy, account?.PasswordHash ?? DummyHash, password);
        if (account == null) return null;
        if (account.Status != "Active" || account.LockedUntilUtc > DateTime.UtcNow) return null;
        if (valid == PasswordVerificationResult.Failed)
        {
            account.FailedLoginCount++;
            if (account.FailedLoginCount >= 5) { account.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5); account.FailedLoginCount = 0; }
            await db.SaveChangesAsync(ct); return null;
        }
        account.FailedLoginCount = 0; account.LockedUntilUtc = null;
        if (valid == PasswordVerificationResult.SuccessRehashNeeded) account.PasswordHash = hasher.HashPassword(account, password);
        await db.SaveChangesAsync(ct); return account;
    }

    public async Task<(string Token, DateTime Expires)> CreateSession(AccountRecord account, CancellationToken ct)
    {
        var token = Token(); var expires = DateTime.UtcNow.AddHours(12);
        var coach = await db.AccountCoaches.SingleAsync(x => x.AccountId == account.Id && x.IsPrimary, ct);
        db.AccountSessions.Add(new() { IdHash = Hash(token), AccountId = account.Id, CoachId = coach.CoachId, ExpiresAtUtc = expires });
        await db.SaveChangesAsync(ct); return (token, expires);
    }

    public async Task<AccountSessionRecord?> Session(string token, CancellationToken ct = default)
    {
        var hash = Hash(token);
        var session = await db.AccountSessions.AsNoTracking().SingleOrDefaultAsync(x => x.IdHash == hash, ct);
        if (session == null || session.RevokedAtUtc != null || session.ExpiresAtUtc <= DateTime.UtcNow) return null;
        return await db.Accounts.AnyAsync(x => x.Id == session.AccountId && x.Status == "Active", ct) ? session : null;
    }

    public Task<AccountRecord> Account(Guid id, CancellationToken ct) => db.Accounts.AsNoTracking().SingleAsync(x => x.Id == id, ct);
    public Task<List<AccountCoachRecord>> Profiles(Guid account, CancellationToken ct) => db.AccountCoaches.AsNoTracking().Where(x => x.AccountId == account).OrderByDescending(x => x.IsPrimary).ThenBy(x => x.CreatedAtUtc).ToListAsync(ct);
    public Task<bool> Owns(Guid account, Guid coach, CancellationToken ct = default) => db.AccountCoaches.AnyAsync(x => x.AccountId == account && x.CoachId == coach, ct);

    public async Task Revoke(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        await db.AccountSessions.Where(x => x.IdHash == hash).ExecuteUpdateAsync(x => x.SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow), ct);
    }

    public async Task<bool> SelectProfile(string token, Guid coach, CancellationToken ct)
    {
        var session = await Session(token, ct);
        if (session == null || !await Owns(session.AccountId, coach, ct)) return false;
        await db.AccountSessions.Where(x => x.IdHash == session.IdHash).ExecuteUpdateAsync(x => x.SetProperty(s => s.CoachId, coach), ct);
        return true;
    }

    public async Task<string?> IssueGrant(string username, Guid coachId, string reason, IWorldRepository worlds, CancellationToken ct)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.NormalizedUsername == Normalize(username), ct);
        if (account == null) return null;
        var exists = (await worlds.ListWorldsAsync(ct)).Any(w => w.HumanCoaches.Any(c => c.Id == coachId));
        if (!exists || await db.AccountCoaches.AnyAsync(x => x.CoachId == coachId, ct))
        { await Audit(account.Id, coachId, "Rejected", "Identidade ausente ou já vinculada.", ct); return null; }
        var token = Token();
        db.AccountLinkGrants.Add(new() { TokenHash = Hash(token), AccountId = account.Id, CoachId = coachId,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30), Reason = reason });
        await Audit(account.Id, coachId, "Issued", reason, ct);
        return token;
    }

    public async Task<bool> ConsumeGrant(Guid accountId, string token, CancellationToken ct)
    {
        var hash = Hash(token);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var grant = await db.AccountLinkGrants.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (grant == null || grant.AccountId != accountId || grant.ConsumedAtUtc != null || grant.ExpiresAtUtc <= DateTime.UtcNow)
        { await transaction.RollbackAsync(ct); await Audit(accountId, null, "Rejected", "Código inválido, expirado ou de outra conta.", ct); return false; }
        var changed = await db.AccountLinkGrants.Where(x => x.TokenHash == hash && x.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(x => x.SetProperty(g => g.ConsumedAtUtc, DateTime.UtcNow), ct);
        if (changed != 1 || await db.AccountCoaches.AnyAsync(x => x.CoachId == grant.CoachId, ct))
        { await transaction.RollbackAsync(ct); await Audit(accountId, grant.CoachId, "Rejected", "Conflito de vínculo.", ct); return false; }
        db.AccountCoaches.Add(new() { AccountId = accountId, CoachId = grant.CoachId, IsPrimary = false,
            DisplayName = "Treinador vinculado", CreatedAtUtc = DateTime.UtcNow });
        try {
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            await Audit(accountId, grant.CoachId, "Linked", grant.Reason, ct); return true;
        } catch (DbUpdateException) {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
            await Audit(accountId, grant.CoachId, "Rejected", "Conflito de vínculo.", ct); return false;
        }
    }

    private async Task Audit(Guid account, Guid? coach, string outcome, string reason, CancellationToken ct)
    {
        db.AccountLinkAudit.Add(new() { Id = Guid.NewGuid(), AccountId = account, CoachId = coach,
            CreatedAtUtc = DateTime.UtcNow, Outcome = outcome, Reason = reason });
        await db.SaveChangesAsync(ct);
    }
}
