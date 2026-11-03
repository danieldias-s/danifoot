using Elifoot.Contracts;
namespace Elifoot.Domain;

/// <summary>Initial catalogue classification only; never player strength or an official world ranking.</summary>
public static class WorldCatalogRules016B
{
    public static int PowerAt(int zeroBasedRank) => Math.Max(1, 20 - Math.Max(0,zeroBasedRank)/2);
    public static int SquadSizeAt(int zeroBasedRank) => 23 - Math.Max(0,zeroBasedRank)%14/2;
    public static List<EligibleCatalogClub> Classify(IEnumerable<EligibleCatalogClub> input)
    {
        var clubs=input.OrderByDescending(c=>c.SourceRanking?.Score ?? c.RankingInfo?.Score ?? double.MinValue)
            .ThenBy(c=>c.SourceRanking?.Rank ?? c.RankingInfo?.Rank ?? int.MaxValue)
            .ThenBy(c=>c.Key,StringComparer.Ordinal).ToList();
        return clubs.Select((c,i)=> {
            var rank=c.SourceRanking ?? c.RankingInfo;
            var manual=c.InitialProfile is {ManualPower:true} old ? old.ClubInitialPower : (int?)null;
            return c with {InitialProfile=new(manual ?? PowerAt(i),i+1,rank?.Score,
                rank?.Origin==CatalogDataOrigin.Estimated?$"Elifoot: {rank.Method} ({rank.Source})":rank?.Source ?? "elifoot-stable-order-unranked",rank?.UpdatedAt ?? DateTime.UnixEpoch,
                Math.Clamp(rank?.Confidence ?? 0,0,1),ManualPower:manual!=null,ManualPowerEvidence:manual!=null?c.InitialProfile?.ManualPowerEvidence:null)};
        }).ToList();
    }
    public static List<EligibleCatalogClub> Order(IEnumerable<EligibleCatalogClub> clubs) => clubs
        .OrderByDescending(c=>c.InitialProfile?.ClubInitialPower ?? 1)
        .ThenByDescending(c=>c.InitialProfile?.WorldRankingScore ?? double.MinValue)
        .ThenBy(c=>c.InitialProfile?.WorldRank ?? int.MaxValue).ThenBy(c=>c.Key,StringComparer.Ordinal).ToList();
    public static List<CatalogAthlete> SelectRoster(IEnumerable<CatalogAthlete> candidates,int target)
    {
        if(target is <11 or >24)throw new ArgumentException("Alvo do elenco deve estar entre 11 e 24.");
        var all=candidates.Where(p=>p.Position is "G" or "D" or "M" or "A")
            .GroupBy(p=>p.CanonicalPlayerId ?? $"{p.Source}:{p.PlayerExternalId}",StringComparer.Ordinal)
            .Select(g=>g.OrderByDescending(p=>p.UpdatedAt).ThenBy(p=>p.PlayerExternalId,StringComparer.Ordinal).First()).ToList();
        double Percentile(double value,IEnumerable<double> source) {var x=source.ToList();return x.Count<2?.5:(x.Count(n=>n<value)+.5*x.Count(n=>n==value))/x.Count;}
        double Score(CatalogAthlete p) {
            var cohort=all.Where(x=>x.Position==p.Position).ToList();
            // Missing information is neutral, not a fabricated zero or automatically last.
            double rating=p.Performance?.Rating is double v?Percentile(v,cohort.Where(x=>x.Performance?.Rating!=null).Select(x=>x.Performance!.Rating!.Value)):.5;
            double minutes=p.Performance?.Minutes is int m?Percentile(m,cohort.Where(x=>x.Performance?.Minutes!=null).Select(x=>(double)x.Performance!.Minutes!.Value)):.5;
            double games=p.Performance?.Appearances is int a?Percentile(a,cohort.Where(x=>x.Performance?.Appearances!=null).Select(x=>(double)x.Performance!.Appearances!.Value)):.5;
            return .5*rating+.35*minutes+.15*games;
        }
        var ordered=all.OrderByDescending(Score).ThenBy(p=>p.CanonicalPlayerId ?? p.PlayerExternalId,StringComparer.Ordinal).ToList();
        var selected=new List<CatalogAthlete>();
        foreach(var (position,minimum) in new[]{("G",target>11?2:1),("D",target>11?4:3),("M",target>11?4:3),("A",target>11?2:1)})
            selected.AddRange(ordered.Where(p=>p.Position==position).Take(minimum));
        foreach(var p in ordered){if(selected.Count>=target)break;if(!selected.Contains(p))selected.Add(p);}
        return selected.OrderBy(p=>p.Position=="G"?0:p.Position=="D"?1:p.Position=="M"?2:3)
            .ThenByDescending(Score).ThenBy(p=>p.PlayerExternalId,StringComparer.Ordinal).ToList();
    }
    public static EligibleCatalogClub Prepare(EligibleCatalogClub club,int ordinal,int seed,bool allowGenerated,int eliteCutoff,
        DivisionTier? fictionalSeedTier=null)
    {
        bool elite=club.InitialProfile is {WorldRank:>0,RankingConfidence:>0} profile && profile.WorldRank<=eliteCutoff;
        int target=elite?23:SquadSizeAt(ordinal);
        var selected=SelectRoster(club.Players,target);
        var prepared=PlayableWorldPolicy.Prepare(club with {Players=selected},seed,allowGenerated,target,requireTarget:true,
            fictionalSeedTier:fictionalSeedTier);
        selected=SelectRoster(prepared.Players,target);
        var initial=club.InitialProfile ?? new(1,ordinal+1,null,"elifoot-stable-order-unranked",DateTime.UnixEpoch,0);
        return prepared with {Players=selected,InitialProfile=initial with {TargetInitialSquadSize=target,EliteRuleApplied=elite}};
    }
}
