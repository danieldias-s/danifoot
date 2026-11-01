using Elifoot.Contracts;
namespace Elifoot.Domain;

/// <summary>Resolve newer provider membership by stable identity, keeping the older claim as a reference.</summary>
public static class CatalogRosterReconciliation016A
{
    public static List<EligibleCatalogClub> Resolve(List<EligibleCatalogClub> clubs) {
        var memberships=clubs.SelectMany(c=>c.Players.Where(p=>p.IdentityOrigin==CatalogDataOrigin.Real&&p.PlayerOrigin==WorldPlayerOrigin.REAL_CURRENT_PROVIDER_REPORTED)
            .Select(p=>(Club:c,Player:p))).GroupBy(x=>(x.Player.Gender,Identity:x.Player.CanonicalPlayerId??x.Player.Source+":"+x.Player.PlayerExternalId));
        var exclusions=new Dictionary<string,HashSet<string>>();
        foreach(var group in memberships.Where(g=>g.Select(x=>x.Club.Key).Distinct().Count()>1)) {
            var at=group.Max(x=>x.Player.UpdatedAt);var newest=group.Where(x=>x.Player.UpdatedAt==at).Select(x=>x.Club.Key).Distinct().ToList();
            foreach(var item in group.Where(x=>newest.Count!=1||x.Club.Key!=newest[0])) {
                if(!exclusions.TryGetValue(item.Club.Key,out var ids))exclusions[item.Club.Key]=ids=new();ids.Add(group.Key.Identity);
            }
        }
        return clubs.Select(c=>!exclusions.TryGetValue(c.Key,out var excluded)?c:c with {
            Players=c.Players.Where(p=>!excluded.Contains(p.CanonicalPlayerId??p.Source+":"+p.PlayerExternalId)).ToList(),
            ReferencePlayers=(c.ReferencePlayers??[]).Concat(c.Players.Where(p=>excluded.Contains(p.CanonicalPlayerId??p.Source+":"+p.PlayerExternalId)).Select(p=>p with{PlayerOrigin=WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE})).ToList(),
            CoverageWarnings=(c.CoverageWarnings??[]).Append("016A: vínculo simultâneo por ID estável; somente a observação mais recente e unívoca é utilizável. Empates exigem revisão.").ToList()
        }).ToList();
    }
}
