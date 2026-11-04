using Microsoft.EntityFrameworkCore;

namespace Elifoot.Infrastructure;

public class WorldRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public int Seed { get; set; }
    public int TotalClubs { get; set; }
    public int SeasonNumber { get; set; }
    public int RoundNumber { get; set; }
    public int RoundState { get; set; }
    public long RoundVersion { get; set; }
    public int CurrentMatchMinute { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class ClubSummaryRecord
{
    public Guid Id { get; set; }
    public Guid GameWorldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DivisionTier { get; set; }
    public decimal CashBalance { get; set; }
    public int LeaguePoints { get; set; }
    public int BuiltCapacity { get; set; }
    public int UsableCapacity { get; set; }
    public double ConditionPercent { get; set; }
}

public class FinancialLedgerRecord
{
    public Guid Id { get; set; }
    public Guid GameWorldId { get; set; }
    public Guid ClubId { get; set; }
    public int SeasonNumber { get; set; }
    public int RoundNumber { get; set; }
    public int Category { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class SeasonHistoryRecord
{
    public Guid Id { get; set; }
    public Guid GameWorldId { get; set; }
    public int SeasonNumber { get; set; }
    public Guid FirstDivisionChampionClubId { get; set; }
    public string FirstDivisionChampionName { get; set; } = string.Empty;
    public Guid CupChampionClubId { get; set; }
    public string CupChampionName { get; set; } = string.Empty;
    public string TopScorerPlayerName { get; set; } = string.Empty;
    public int TopScorerGoals { get; set; }
}

public class ElifootDbContext : DbContext
{
    public ElifootDbContext(DbContextOptions<ElifootDbContext> options) : base(options)
    {
    }

    public DbSet<WorldRecord> Worlds => Set<WorldRecord>();
    public DbSet<ClubSummaryRecord> ClubSummaries => Set<ClubSummaryRecord>();
    public DbSet<FinancialLedgerRecord> FinancialLedger => Set<FinancialLedgerRecord>();
    public DbSet<SeasonHistoryRecord> SeasonHistories => Set<SeasonHistoryRecord>();
    public DbSet<AccountRecord> Accounts => Set<AccountRecord>();
    public DbSet<AccountCoachRecord> AccountCoaches => Set<AccountCoachRecord>();
    public DbSet<AccountSessionRecord> AccountSessions => Set<AccountSessionRecord>();
    public DbSet<LegacyAccountGrantRecord> AccountLinkGrants => Set<LegacyAccountGrantRecord>();
    public DbSet<AccountLinkAuditRecord> AccountLinkAudit => Set<AccountLinkAuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountRecord>(b => {
            b.ToTable("ef_accounts"); b.HasKey(x => x.Id);
            b.HasIndex(x => x.NormalizedUsername).IsUnique();
            b.Property(x => x.NormalizedUsername).HasMaxLength(80);
            b.Property(x => x.PasswordHash).HasMaxLength(1000);
        });
        modelBuilder.Entity<AccountCoachRecord>(b => {
            b.ToTable("ef_account_coaches"); b.HasKey(x => x.CoachId);
            b.HasIndex(x => x.AccountId);
            b.HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AccountSessionRecord>(b => {
            b.ToTable("ef_account_sessions"); b.HasKey(x => x.IdHash);
            b.HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AccountCoachRecord>().WithMany().HasForeignKey(x => x.CoachId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LegacyAccountGrantRecord>(b => {
            b.ToTable("ef_account_link_grants"); b.HasKey(x => x.TokenHash);
            b.HasOne<AccountRecord>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AccountLinkAuditRecord>(b => { b.ToTable("ef_account_link_audit"); b.HasKey(x => x.Id); });
        modelBuilder.Entity<WorldRecord>(b =>
        {
            b.ToTable("ef_worlds");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.InviteCode).IsUnique();
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.InviteCode).HasMaxLength(32).IsRequired();
            b.Property(x => x.RoundVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<ClubSummaryRecord>(b =>
        {
            b.ToTable("ef_club_summaries");
            b.HasKey(x => new { x.GameWorldId, x.Id });
            b.HasIndex(x => new { x.GameWorldId, x.DivisionTier });
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<FinancialLedgerRecord>(b =>
        {
            b.ToTable("ef_financial_ledger");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.GameWorldId, x.ClubId, x.IdempotencyKey }).IsUnique();
            b.HasIndex(x => new { x.GameWorldId, x.SeasonNumber, x.RoundNumber });
            b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(400);
        });

        modelBuilder.Entity<SeasonHistoryRecord>(b =>
        {
            b.ToTable("ef_season_histories");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.GameWorldId, x.SeasonNumber }).IsUnique();
        });
    }
}
