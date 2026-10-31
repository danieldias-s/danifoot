using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Infrastructure.Catalog;

public sealed class ClubColorCache
{
    public Guid ClubId {get;set;}
    public string PaletteJson {get;set;}="";
    public DateTime? LastAttempt {get;set;}
    public DateTime? NextAttempt {get;set;}
    public string? LastIssue {get;set;}
}
public sealed class PlayerAppearance
{
    public Guid OwnerId {get;set;} public Guid WorldId {get;set;} public Guid ClubId {get;set;}
    public string PreferencesJson {get;set;}="{}";public int Version {get;set;}
    public DateTime UpdatedAtUtc {get;set;}
}
public interface IClubColorSource
{
    Task<ClubColorPalette?> FindAsync(EligibleCatalogClub club,CancellationToken ct=default);
}
public sealed class ClubColorService(IServiceScopeFactory factory,IEnumerable<IClubColorSource> sources,IWorldRepository worlds)
{
    private readonly SemaphoreSlim cacheGate = new(1,1);
    public static async Task EnsureSchema(CatalogDbContext db,CancellationToken ct=default)
    {
        bool pg=db.Database.IsNpgsql();string guid=pg?"uuid":"TEXT",date=pg?"timestamp with time zone":"TEXT";
        // All interpolated values are fixed provider SQL types; never user input.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"""
          CREATE TABLE IF NOT EXISTS "wc013a_ClubColorCache" ("ClubId" {guid} PRIMARY KEY,"PaletteJson" TEXT NOT NULL,
          "LastAttempt" {date} NULL,"NextAttempt" {date} NULL,"LastIssue" TEXT NULL);
          CREATE TABLE IF NOT EXISTS "wc013a_PlayerAppearance" ("OwnerId" {guid} NOT NULL,"WorldId" {guid} NOT NULL,
          "ClubId" {guid} NOT NULL,"PreferencesJson" TEXT NOT NULL,"Version" INTEGER NOT NULL,"UpdatedAtUtc" {date} NOT NULL,
          PRIMARY KEY("OwnerId","WorldId","ClubId"));
          """,ct);
#pragma warning restore EF1002
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,CatalogImportCodec.Json);
    public async Task<List<EligibleCatalogClub>> Enrich(List<EligibleCatalogClub> clubs,CancellationToken ct=default)
    {
        using var scope=factory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        // Test harnesses that only exercise the original creator do not register the optional color service.
        var ids=clubs.Select(c=>c.InternalCatalogClubId).Where(id=>id!=null).Select(id=>id!.Value).ToList();
        var rows=await db.Set<ClubColorCache>().AsNoTracking().Where(c=>ids.Contains(c.ClubId)).ToListAsync(ct);
        return clubs.Select(c=>{
            var json=rows.FirstOrDefault(r=>r.ClubId==c.InternalCatalogClubId)?.PaletteJson;
            var palette=string.IsNullOrWhiteSpace(json)?c.ColorPalette:JsonSerializer.Deserialize<ClubColorPalette>(json,CatalogImportCodec.Json);
            if(palette==null)return c;
            if(palette.CountryCode!=c.CountryCode||palette.Gender!=c.Gender)return c;
            return c with {PrimaryColor=palette.PrimaryColor,SecondaryColor=palette.SecondaryColor??palette.PrimaryColor,ColorPalette=palette};
        }).ToList();
    }
    public async Task<List<ClubColorCoverageDto>> Refresh(List<EligibleCatalogClub> clubs,bool force=false,CancellationToken ct=default)
    {
        await cacheGate.WaitAsync(ct);
        try {
        var report=new List<ClubColorCoverageDto>();
        foreach(var club in clubs)
        {
            if(club.InternalCatalogClubId is not Guid id)continue;
            using var scope=factory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var row=await db.Set<ClubColorCache>().SingleOrDefaultAsync(r=>r.ClubId==id,ct);
            row??=new(){ClubId=id};bool exists=db.Entry(row).State!=EntityState.Detached;
            if(force||row.NextAttempt==null||row.NextAttempt<=DateTime.UtcNow)
            {
                row.LastAttempt=DateTime.UtcNow;row.LastIssue=null;ClubColorPalette? palette=club.ColorPalette;
                try {
                    if(palette!=null)ClubColorPolicy.Validate(palette);
                    bool failed=false;
                    foreach(var source in sources){
                        if(palette!=null)break;
                        try {
                            var candidate=await source.FindAsync(club,ct);
                            if(candidate!=null){ClubColorPolicy.Validate(candidate);if(candidate.CountryCode!=club.CountryCode||candidate.Gender!=club.Gender||candidate.Provider!=club.Provider||candidate.ExternalId!=club.ExternalId)throw new ArgumentException("Identidade da fonte incompatível.");palette=candidate;}
                        } catch(Exception e) when(e is HttpRequestException or ArgumentException or TaskCanceledException or JsonException){
                            if(ct.IsCancellationRequested)throw;
                            failed=true;row.LastIssue="Fonte indisponível ou inválida; último resultado válido preservado.";
                        }
                    }
                    if(palette!=null){ClubColorPolicy.Validate(palette);if(palette.CountryCode!=club.CountryCode||palette.Gender!=club.Gender||palette.Provider!=club.Provider||palette.ExternalId!=club.ExternalId)throw new ArgumentException("Identidade da paleta diverge do clube.");row.PaletteJson=Json(palette);row.LastIssue=null;row.NextAttempt=DateTime.UtcNow.AddDays(30);}
                    else{row.LastIssue??="Sem paleta tradicional verificada; tema neutro.";row.NextAttempt=failed?DateTime.UtcNow.AddHours(6):DateTime.UtcNow.AddDays(7);}
                } catch(Exception e) when(e is HttpRequestException or ArgumentException or TaskCanceledException or JsonException) {
                    if(ct.IsCancellationRequested)throw;
                    row.LastIssue="Fonte indisponível ou inválida; último resultado válido preservado.";row.NextAttempt=DateTime.UtcNow.AddHours(6);
                }
                if(!exists)db.Add(row);await db.SaveChangesAsync(ct);
            }
            var stored=string.IsNullOrWhiteSpace(row.PaletteJson)?null:JsonSerializer.Deserialize<ClubColorPalette>(row.PaletteJson,CatalogImportCodec.Json);
            report.Add(new(id,club.CountryCode,club.Gender,club.Name,stored,stored==null?"MISSING":stored.ColorVerified?"VERIFIED":"USER_DEFINED",row.LastAttempt,row.LastIssue));
        }
        return report;
        } finally { cacheGate.Release(); }
    }
    public async Task Import(ImportClubColorRequest request,CancellationToken ct=default)
    {
        await cacheGate.WaitAsync(ct);
        try {
        ClubColorPolicy.Validate(request.Palette);
        if(request.Palette.CountryCode!=request.CountryCode||request.Palette.Gender!=request.Gender||request.Palette.Provider!=request.Provider||request.Palette.ExternalId!=request.ExternalId)
            throw new ArgumentException("Identidade da importação de cores incompatível.");
        using var scope=factory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var key=CatalogIdentityService.AliasKey(request.CountryCode,request.Gender,request.Provider,request.ExternalId);
        var alias=await db.Set<CatalogClubIdentityAlias>().AsNoTracking().SingleOrDefaultAsync(a=>a.Key==key,ct)??throw new ArgumentException("Associe o ID externo ao clube antes de importar cores.");
        var row=await db.Set<ClubColorCache>().SingleOrDefaultAsync(c=>c.ClubId==alias.ClubId,ct);
        if(row==null){row=new(){ClubId=alias.ClubId};db.Add(row);}
        row.PaletteJson=Json(request.Palette);row.LastAttempt=DateTime.UtcNow;row.NextAttempt=DateTime.UtcNow.AddDays(30);row.LastIssue=null;await db.SaveChangesAsync(ct);
        } finally { cacheGate.Release(); }
    }
    private async Task<(GameWorld World,Club Club)> Club(Guid owner,Guid worldId,Guid clubId,bool requireCurrent,CancellationToken ct)
    {
        var world=await worlds.GetWorldAsync(worldId,ct)??throw new KeyNotFoundException("Mundo não encontrado.");
        var coach=world.HumanCoaches.FirstOrDefault(h=>h.Id==owner)??throw new KeyNotFoundException("Treinador não participa deste mundo.");
        var club=world.Clubs.FirstOrDefault(c=>c.Id==clubId)??throw new KeyNotFoundException("Clube não encontrado.");
        if(requireCurrent&&club.HumanCoachId!=owner)throw new ArgumentException("Configure a aparência do clube que você controla atualmente.");
        return(world,club);
    }
    public async Task<AppearanceStateDto> Appearance(Guid owner,Guid worldId,Guid clubId,SaveAppearanceRequest? command=null,CancellationToken ct=default)
    {
        var (_,club)=await Club(owner,worldId,clubId,command!=null,ct);
        using var scope=factory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var row=await db.Set<PlayerAppearance>().SingleOrDefaultAsync(a=>a.OwnerId==owner&&a.WorldId==worldId&&a.ClubId==clubId,ct);
        if(command!=null){
            ClubColorPolicy.Validate(command.Preferences);
            if((row?.Version??0)!=command.ExpectedVersion)throw new AssemblyConflictException("A aparência mudou em outra janela. Recarregue antes de salvar.");
            if(row==null){row=new(){OwnerId=owner,WorldId=worldId,ClubId=clubId};db.Add(row);}
            row.Version++;row.PreferencesJson=Json(command.Preferences with{Version=row.Version});row.UpdatedAtUtc=DateTime.UtcNow;
            try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){throw new AssemblyConflictException("A aparência mudou em outra janela. Recarregue antes de salvar.");}
        }
        return new(worldId,clubId,club.ColorPalette,row==null?new():JsonSerializer.Deserialize<AppearancePreferencesDto>(row.PreferencesJson,CatalogImportCodec.Json)!);
    }
}
