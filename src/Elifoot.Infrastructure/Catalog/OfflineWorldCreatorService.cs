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

public sealed class OfflineWorldCreatorService(OfflineCatalogLoader loader, IServiceScopeFactory factory, IWorldRepository worlds)
{
    private sealed record Envelope(string OfflineCatalogVersion, OfflineWorldRequest Request);
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    public Task<OfflineCatalogReport> Catalogs(CancellationToken ct = default) => loader.LoadAsync(ct);

    public Task<List<EligibleCatalogClub>> Select(List<string> ids, CancellationToken ct = default) =>
        SelectCatalogs(ids, planningOnly: false, ct);

    public Task<List<EligibleCatalogClub>> SelectForPlanning(List<string> ids, CancellationToken ct = default) =>
        SelectCatalogs(ids, planningOnly: true, ct);

    public static bool CanUseDefaultBehavior(OfflineLoadedCatalog catalog) =>
        catalog.Manifest is { SchemaVersion: "1.1", Status: "DRAFT" } &&
        !string.IsNullOrWhiteSpace(catalog.Manifest.Name) && catalog.Valid &&
        catalog.Clubs.Count > 0 && catalog.Clubs.All(club => club.V11 != null) &&
        catalog.Issues.Count > 0 && catalog.Issues.All(issue => issue.Code == "BEHAVIOR");

    private async Task<List<EligibleCatalogClub>> SelectCatalogs(List<string> ids, bool planningOnly, CancellationToken ct,
        bool includePreviewRosters = false, bool useDefaultBehavior = false)
    {
        if (ids == null || ids.Count == 0 || ids.Distinct().Count() != ids.Count) throw new ArgumentException("Selecione catálogos sem repetição.");
        var report = await loader.LoadAsync(ct);
        if (report.Issues.Any(i => i.IsError)) throw new ArgumentException(string.Join("; ", report.Issues.Select(i => i.Message)));
        var clubs = new List<EligibleCatalogClub>();
        foreach (string id in ids)
        {
            var matches = report.Catalogs.Where(c => c.Manifest?.CatalogId == id).ToList();
            if (matches.Count != 1) throw new ArgumentException($"Catálogo inexistente ou duplicado: {id}.");
            var catalog = matches[0];
            var useDefaults = useDefaultBehavior && CanUseDefaultBehavior(catalog);
            if (!catalog.Valid || !planningOnly && !catalog.Ready && !useDefaults)
                throw new ArgumentException($"Catálogo {id} incompleto/inválido. Corrija os arquivos e marque READY após validar.");
            if (includePreviewRosters && catalog.Issues.FirstOrDefault(i => i.Code is not ("BEHAVIOR" or "RANKING_RESOLUTION")) is { } issue)
                throw new ArgumentException($"Catálogo {id}: {issue.Message} ({issue.Path})");
            var candidates = catalog.Ready || useDefaults ? catalog.Clubs : catalog.Clubs
                .Where(c => OfflineCatalogRules.Text(c.Definition.Name) && OfflineCatalogRules.Text(c.Definition.ShortName, 80) &&
                    c.Definition.Colors is { Count: 2 } colors && colors.All(color => !string.IsNullOrWhiteSpace(color)));
            if (!candidates.Any()) throw new ArgumentException($"Catálogo {id} não tem clubes aptos para montar as divisões.");
            clubs.AddRange(candidates.Select(c => OfflineWorldFactory.Eligible(c, catalog.Manifest!.Gender, planningOnly && !includePreviewRosters)));
        }
        if (clubs.Select(c => c.CountryCode).Distinct().Count() > 8 || clubs.Select(c => c.Gender).Distinct().Count() != 1)
            throw new ArgumentException("Escolha uma categoria e até oito países.");
        return OfflineCatalogRanking.Order(clubs);
    }

    public async Task<WorldCreatorPreview> PreviewForPlanning(OfflineWorldRequest request, CancellationToken ct = default)
    {
        var candidates = await SelectCatalogs(request.CatalogIds, planningOnly: true, ct, includePreviewRosters: true);
        var preview = OfflineWorldFactory.Preview(request, candidates);
        return preview with
        {
            DraftId = Guid.Empty,
            Errors = [.. preview.Errors, "Prévia de rascunho. Valide o catálogo para confirmar a criação do campeonato."]
        };
    }

    public async Task<WorldCreatorPreview> Preview(Guid owner, OfflineWorldRequest request, CancellationToken ct = default)
    {
        var candidates = await SelectCatalogs(request.CatalogIds, planningOnly: false, ct,
            useDefaultBehavior: request.UseDefaultBehavior);
        var preview = OfflineWorldFactory.Preview(request, candidates);
        using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Drafts.Add(new()
        {
            Id = preview.DraftId, OwnerId = owner, ExpiresAt = DateTime.UtcNow.AddHours(2),
            RequestJson = JsonSerializer.Serialize(new Envelope(OfflineCatalogRules.Version, request), CatalogImportCodec.Json),
            PreviewJson = JsonSerializer.Serialize(preview, CatalogImportCodec.Json)
        });
        await db.SaveChangesAsync(ct);
        return preview;
    }

    public async Task<GameWorld> Confirm(Guid owner, Guid id, CancellationToken ct = default)
    {
        var gate = Gates.GetOrAdd(id, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            bool postgres = db.Database.IsNpgsql(), locked = false;
            long key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes($"offline-confirm:{id:N}")));
            try
            {
                if (postgres)
                {
                    await db.Database.OpenConnectionAsync(ct);
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({key})", ct); locked = true;
                }
                var draft = await db.Drafts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == owner, ct)
                    ?? throw new KeyNotFoundException("Prévia não encontrada para este usuário.");
                using var tagged = JsonDocument.Parse(draft.RequestJson);
                if (!tagged.RootElement.TryGetProperty("offlineCatalogVersion", out var version) || version.GetString() != OfflineCatalogRules.Version)
                    throw new ArgumentException("Use a confirmação correspondente ao tipo da prévia.");
                if (draft.ExpiresAt < DateTime.UtcNow && draft.WorldJson == null) throw new ArgumentException("Prévia expirada.");
                if (draft.WorldJson == null)
                {
                    var envelope = JsonSerializer.Deserialize<Envelope>(draft.RequestJson, CatalogImportCodec.Json)!;
                    var preview = JsonSerializer.Deserialize<WorldCreatorPreview>(draft.PreviewJson, CatalogImportCodec.Json)!;
                    var world = OfflineWorldFactory.Create(envelope.Request, preview);
                    world.InitialLobby = new() { HostCoachId = owner };
                    string json = JsonSerializer.Serialize(world, CatalogImportCodec.Json);
                    await db.Drafts.Where(d => d.Id == id && d.OwnerId == owner && d.WorldJson == null)
                        .ExecuteUpdateAsync(d => d.SetProperty(x => x.WorldId, world.Id).SetProperty(x => x.WorldJson, json), ct);
                    draft = await db.Drafts.AsNoTracking().SingleAsync(d => d.Id == id, ct);
                }
                var existing = await worlds.GetWorldAsync(draft.WorldId!.Value, ct);
                if (existing != null) return existing;
                var created = JsonSerializer.Deserialize<GameWorld>(draft.WorldJson!, CatalogImportCodec.Json)!;
                await worlds.SaveWorldAsync(created, ct);
                return created;
            }
            finally
            {
                if (postgres)
                {
                    try { if (locked) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({key})", CancellationToken.None); }
                    finally { await db.Database.CloseConnectionAsync(); }
                }
            }
        }
        finally { gate.Release(); }
    }
}
