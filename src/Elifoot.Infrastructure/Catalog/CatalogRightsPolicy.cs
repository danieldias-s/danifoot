using Elifoot.Contracts;

namespace Elifoot.Infrastructure.Catalog;

public static class CatalogRightsPolicy
{
    public static string? StorageIssue(CatalogImportPackage package, DateTime now)
    {
        if (package.IsFixture) return null;
        var r = package.Rights;
        if (r == null || !r.AccessAllowed || !r.StorageAllowed || string.IsNullOrWhiteSpace(r.License) || string.IsNullOrWhiteSpace(r.Evidence))
            return "Dados reais exigem evidência de acesso e armazenamento autorizado.";
        if (r.RetainUntil != null) return "Este catálogo permanente não armazena fontes com retenção limitada; use outra fonte autorizada.";
        if (r.UseUntil <= now) return "Licença/direito de retenção expirado.";
        return null;
    }
    public static string? WorldIssue(CatalogReference reference, DateTime now)
    {
        if (reference.IsFixture) return null;
        var r = reference.Rights;
        if (r == null || !r.DisplayAllowed || !r.CommercialUseAllowed || !r.OfflineWorldsAllowed || r.RetainUntil != null || r.UseUntil <= now)
            return $"{reference.Scope.Provider}: direitos insuficientes/expirados para um mundo independente permanente.";
        return null;
    }
    public static CatalogUsageRights WikidataRights => new("CC0-1.0", "https://www.wikidata.org/wiki/Wikidata:Licensing", true, true,
        true, true, true, true, true, "Wikidata (CC0), contribuições comunitárias; cobertura não garantida.");
}
