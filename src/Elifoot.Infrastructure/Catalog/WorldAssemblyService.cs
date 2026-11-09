using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Infrastructure.Catalog;

public sealed class AssemblyConflictException(string message) : Exception(message);

public sealed class WorldAssemblyService(IServiceScopeFactory factory, IWorldRepository worlds)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    public static async Task EnsureSchema(CatalogDbContext db, CancellationToken ct = default)
    {
        bool pg = db.Database.IsNpgsql(); string guid = pg ? "uuid" : "TEXT", date = pg ? "timestamp with time zone" : "TEXT";
        string boolean = pg ? "boolean" : "INTEGER", no = pg ? "FALSE" : "0";
        // Only fixed SQL type names above are interpolated, never user input.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"""
            CREATE TABLE IF NOT EXISTS "wc013a_CustomWorldAssembly" (
              "Id" {guid} PRIMARY KEY, "OwnerId" {guid} NOT NULL, "Name" TEXT NOT NULL,
              "CountriesJson" TEXT NOT NULL, "SourceJson" TEXT NOT NULL, "PackageJson" TEXT NOT NULL,
              "Version" INTEGER NOT NULL, "IsSaved" {boolean} NOT NULL DEFAULT {no},
              "TrustedCatalog" {boolean} NOT NULL DEFAULT {no}, "Personalized" {boolean} NOT NULL DEFAULT {no},
              "CreatedAtUtc" {date} NOT NULL, "UpdatedAtUtc" {date} NOT NULL);
            """, ct);
#pragma warning restore EF1002
    }
    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, CatalogImportCodec.Json)!;
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, CatalogImportCodec.Json);
    private static async Task<List<CatalogCountryEntry>> Countries(CatalogDbContext db, List<string> codes, CancellationToken ct)
    {
        if (codes == null || codes.Count == 0 || codes.Count > 64 || codes.Distinct().Count() != codes.Count) throw new ArgumentException("Selecione países válidos sem repetição.");
        var countries = await db.Countries.AsNoTracking().Where(c => codes.Contains(c.Code)).ToListAsync(ct);
        if (countries.Count != codes.Count) throw new ArgumentException("País não encontrado no catálogo.");
        return countries.Select(c => new CatalogCountryEntry(c.Code, c.Name)).ToList();
    }
    private static List<CatalogCountryEntry> SnapshotCountryNames(List<CatalogCountryEntry> countries, WorldCreationOrigin? origin)
    {
        if (origin?.CatalogYears == null) return countries;
        return countries.Select(country => new CatalogCountryEntry(country.Code,
            origin.Clubs.FirstOrDefault(entry => entry.Club.CountryCode == country.Code)?.Club.CountryName ?? country.Name)).ToList();
    }
    private static void Shape(WorldPackageExportDto package)
    { if (package.Clubs == null || package.Players == null || package.AiManagers == null) throw new ArgumentException("Pacote incompleto: informe clubes, jogadores e técnicos."); }
    private static void Name(string value)
    { if (string.IsNullOrWhiteSpace(value) || value.Length > 160) throw new ArgumentException("Informe um nome de montagem de até 160 caracteres."); }
    private static async Task<CustomWorldAssembly> Owned(CatalogDbContext db, Guid owner, Guid id, CancellationToken ct) =>
        await db.Assemblies.SingleOrDefaultAsync(a => a.Id == id && a.OwnerId == owner, ct) ?? throw new KeyNotFoundException("Montagem não encontrada para este usuário.");
    private static async Task<WorldAssemblyDto> Dto(CatalogDbContext db, CustomWorldAssembly row, CancellationToken ct)
    {
        var countries = Read<List<string>>(row.CountriesJson); var available = await Countries(db, countries, ct);
        var package = Read<WorldPackageExportDto>(row.PackageJson); var baseline = Read<WorldPackageExportDto>(row.SourceJson);
        if (row.TrustedCatalog) available = SnapshotCountryNames(available, baseline.CatalogOrigin);
        return new(row.Id, row.Name, package.Gender, countries, package.Seed, package, row.Version, row.IsSaved, row.Personalized,
            CustomAssemblyPolicy.Validate(package, available, baseline, row.Personalized));
    }
    public async Task<WorldAssemblyDto> Create(Guid owner, CreateWorldAssemblyRequest request, CancellationToken ct = default)
    {
        if(request.UseWorldCatalogRules&&request.Countries.Count is <1 or >8)throw new ArgumentException("Selecione de 1 a 8 países.");
        Name(request.Name); if (!Enum.IsDefined(request.Gender)) throw new ArgumentException("Categoria inválida.");
        using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var countries = await Countries(db, request.Countries, ct); WorldPackageExportDto source; bool trusted = false;
        if (request.SourcePreviewId is Guid previewId)
        {
            var draft = await db.Drafts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == previewId && d.OwnerId == owner, ct) ?? throw new KeyNotFoundException("Prévia não encontrada para este usuário.");
            if (draft.ExpiresAt < DateTime.UtcNow) throw new ArgumentException("A prévia expirou; gere outra antes de editar.");
            var preview = Read<WorldCreatorPreview>(draft.PreviewJson); var config = Read<WorldCreatorRequest>(draft.RequestJson);
            foreach (var reference in preview.References)
                if (CatalogRightsPolicy.WorldIssue(reference, DateTime.UtcNow, config.CatalogYears != null) is string issue)
                    throw new ArgumentException(issue);
            source = WorldEditorAndPackageService.ExportWorldToPackage(WorldCreatorEngine.Create(config, preview)); trusted = true;
        }
        else if (request.Package != null) { Shape(request.Package); source = request.Package; if (!request.AllowPersonalizedContent) throw new ArgumentException("Autorize o conteúdo personalizado para importar uma montagem."); }
        else
        {
            if (!request.AllowPersonalizedContent) throw new ArgumentException("Autorize explicitamente clubes e jogadores personalizados.");
            var template = WorldSeedFactory.CreateSeededWorld(request.Name, request.Seed, 128, forceEvolutionMode: ForceEvolutionMode.ClassicAdapted);
            if(request.UseWorldCatalogRules) {
                var configuration=new WorldCreatorRequest(request.Name,request.Gender,DateTime.UtcNow.Year.ToString(),request.Countries,
                    WorldAssemblyMode.Automatic,DistrictFillMode.Ranking,request.Seed,[],AllowFictionalTeams:true,AllowFictionalPlayers:true,
                    DataMode:WorldDataMode.FREE_WORLD,UseWorldCatalogRules:true,FictionalOnly:true);
                template=WorldCreatorEngine.Create(configuration,WorldCreatorEngine.Preview(configuration,new(request.Gender,configuration.Season,request.Countries,[],[],[])));
            }
            source = WorldEditorAndPackageService.ExportWorldToPackage(template) with { Gender = request.Gender };
            source = source with { Players = source.Players.Where(p => p.ClubId != null).ToList() };
            source = source with { Clubs = source.Clubs.Select((c, i) => c with { Country = countries[i % countries.Count].Name }).ToList() };
        }
        Shape(source);
        if (trusted) countries = SnapshotCountryNames(countries, source.CatalogOrigin);
        var row = new CustomWorldAssembly
        {
            Id = Guid.NewGuid(),
            OwnerId = owner,
            Name = request.Name.Trim(),
            CountriesJson = Json(request.Countries),
            SourceJson = Json(source),
            Version = 1,
            TrustedCatalog = trusted,
            Personalized = request.AllowPersonalizedContent,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        row.PackageJson = Json(CustomAssemblyPolicy.Prepare(source, source, row.Id, countries, trusted, row.Personalized));
        db.Assemblies.Add(row); await db.SaveChangesAsync(ct); return await Dto(db, row, ct);
    }
    public async Task<WorldAssemblyDto> Get(Guid owner, Guid id, CancellationToken ct = default)
    { using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>(); return await Dto(db, await Owned(db, owner, id, ct), ct); }
    public async Task<List<WorldAssemblySummaryDto>> List(Guid owner, CancellationToken ct = default)
    {
        using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var rows = await db.Assemblies.AsNoTracking().Where(a => a.OwnerId == owner && a.IsSaved).OrderByDescending(a => a.UpdatedAtUtc).ToListAsync(ct);
        return rows.Select(r => { var p = Read<WorldPackageExportDto>(r.PackageJson); return new WorldAssemblySummaryDto(r.Id, r.Name, p.Gender, Read<List<string>>(r.CountriesJson), p.Seed, r.Version, r.UpdatedAtUtc); }).ToList();
    }
    public async Task<WorldAssemblyDto> Update(Guid owner, Guid id, UpdateWorldAssemblyRequest request, CancellationToken ct = default)
    {
        Name(request.Name); Shape(request.Package); using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var row = await Owned(db, owner, id, ct); if (row.Version != request.ExpectedVersion) throw new AssemblyConflictException("A montagem foi alterada em outra janela. Recarregue para comparar; suas alterações locais foram mantidas.");
        var countries = await Countries(db, request.Countries, ct); var source = Read<WorldPackageExportDto>(row.SourceJson);
        if (row.TrustedCatalog) countries = SnapshotCountryNames(countries, source.CatalogOrigin);
        row.PackageJson = Json(CustomAssemblyPolicy.Prepare(request.Package, source, id, countries, row.TrustedCatalog, request.AllowPersonalizedContent));
        row.Name = request.Name.Trim(); row.CountriesJson = Json(request.Countries); row.Personalized = request.AllowPersonalizedContent; row.IsSaved |= request.SavePreset; row.Version++; row.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new AssemblyConflictException("A montagem mudou em outra janela. As alterações locais não foram descartadas."); }
        return await Dto(db, row, ct); // Invalid drafts remain editable and private, but cannot create a world.
    }
    public async Task<GameWorld> Confirm(Guid owner, Guid id, ConfirmWorldAssemblyRequest command, CancellationToken ct = default)
    {
        if(command.GameModeId!="CLASSIC_SYNC")throw new ArgumentException("Modo de jogo não implementado.");
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 160 || command.MaxHumans is < 1 or > 8) throw new ArgumentException("Confirmação inválida.");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"assembly:{owner:N}:{id:N}:{command.IdempotencyKey}")); var receiptId = new Guid(bytes.AsSpan(0, 16));
        var gate = Gates.GetOrAdd(receiptId, _ => new SemaphoreSlim(1, 1)); await gate.WaitAsync(ct);
        try
        {
            using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>(); var row = await Owned(db, owner, id, ct);
            var receipt = await db.Drafts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == receiptId && d.OwnerId == owner, ct);
            if (receipt == null)
            {
                var dto = await Dto(db, row, ct); if (!dto.Validation.IsValid) throw new ArgumentException(string.Join("; ", dto.Validation.Errors));
                var origin = dto.Package.CatalogOrigin;
                foreach (var reference in origin?.References ?? [])
                    if (CatalogRightsPolicy.WorldIssue(reference, DateTime.UtcNow, row.TrustedCatalog && origin?.CatalogYears != null) is string issue)
                        throw new ArgumentException(issue);
                string name = string.IsNullOrWhiteSpace(command.WorldName) ? row.Name : command.WorldName.Trim(); Name(name);
                var created = CustomAssemblyPolicy.CreateIndependent(dto.Package, row.Id, row.Version, dto.Countries, name, command.MaxHumans);
                created.GameModeId=command.GameModeId;
                if(command.RequireHostStart)created.InitialLobby=new(){HostCoachId=owner};
                receipt = new() { Id = receiptId, OwnerId = owner, RequestJson = Json(new { assemblyConfirmation = true, assemblyId = id, version = row.Version }), PreviewJson = "{}", WorldId = created.Id, WorldJson = Json(created), ExpiresAt = DateTime.UtcNow.AddYears(100) };
                db.Drafts.Add(receipt);
                try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { db.ChangeTracker.Clear(); receipt = await db.Drafts.AsNoTracking().SingleAsync(d => d.Id == receiptId && d.OwnerId == owner, ct); }
            }
            var world = await worlds.GetWorldAsync(receipt.WorldId!.Value, ct);
            if (world == null) { world = Read<GameWorld>(receipt.WorldJson!); await worlds.SaveWorldAsync(world, ct); }
            return world;
        }
        finally { gate.Release(); }
    }
}
