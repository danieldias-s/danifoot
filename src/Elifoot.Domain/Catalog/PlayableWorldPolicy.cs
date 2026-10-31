using System.Security.Cryptography;
using System.Text;
using Elifoot.Contracts;
namespace Elifoot.Domain;

public static class PlayableWorldPolicy
{
    public static bool Fictional(EligibleCatalogClub club) => club.IsFictional ||
        club.Verification?.Status == CatalogClubVerificationStatus.FICTIONAL_CLUB ||
        club.References.Count > 0 && club.References.All(r => r.IsFixture);
    public static bool Verified(EligibleCatalogClub club) => !Fictional(club) &&
        club.Verification?.Status == CatalogClubVerificationStatus.REAL_CLUB_VERIFIED;
    private static bool Linked(CatalogAthlete p, EligibleCatalogClub club) =>
        p.ClubProvider == club.Provider && p.ClubExternalId == club.ExternalId ||
        club.Aliases?.Any(a => a.Provider == p.ClubProvider && a.ExternalId == p.ClubExternalId) == true;
    public static bool Current(CatalogAthlete p, EligibleCatalogClub club, DateOnly asOf) =>
        Verified(club) && p.IdentityOrigin == CatalogDataOrigin.Real && p.Gender == club.Gender && p.Season == club.Season &&
        Linked(p, club) && p.MembershipEvidence is { Status: CatalogMembershipStatus.DatedActive } proof &&
        !string.IsNullOrWhiteSpace(proof.StatementId) && !string.IsNullOrWhiteSpace(proof.Evidence) && proof.AsOf <= asOf &&
        CatalogMembershipPolicy.Classify(p.MembershipStart, p.MembershipEnd, asOf) == CatalogMembershipStatus.DatedActive &&
        Enum.TryParse<PlayerPosition>(p.Position, out var position) && Enum.IsDefined(position) && !string.IsNullOrWhiteSpace(p.PlayerName);
    public static List<CatalogAthlete> CurrentPlayers(EligibleCatalogClub club, DateOnly asOf) =>
        club.Players.Where(p => Current(p, club, asOf)).Select(p => p with { PlayerOrigin = WorldPlayerOrigin.REAL_VERIFIED_CURRENT }).ToList();
    public static bool Complete(IEnumerable<CatalogAthlete> players)
    {
        var p = players.ToList();
        return p.Count >= 11 && p.Any(x => x.Position == "G") && p.Any(x => x.Position == "D") && p.Count(x => x.Position != "G") >= 10;
    }
    private static ulong Key(int seed, string key) => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(
        SHA256.HashData(Encoding.UTF8.GetBytes($"013c:{seed}:{key}")));
    private static bool Sporting(CatalogRank? rank) => rank is { Rank: > 0, Confidence: > 0 } &&
        rank.Method?.Contains("Identifier", StringComparison.OrdinalIgnoreCase) != true &&
        rank.Method?.StartsWith("Unranked", StringComparison.OrdinalIgnoreCase) != true &&
        rank.Method?.StartsWith("ProvisionalSeeded", StringComparison.OrdinalIgnoreCase) != true;

    public static List<EligibleCatalogClub> Select(WorldCreatorRequest request, CatalogSelection selection, DateOnly asOf,
        List<string> errors, List<string> warnings)
    {
        if (!Enum.IsDefined(request.DataMode!.Value)) { errors.Add("Modo de dados inválido."); return []; }
        if (request.DataMode == WorldDataMode.REAL_COMPLETE && (request.AllowFictionalPlayers || request.AllowFictionalTeams))
            errors.Add("SOMENTE DADOS REAIS COMPLETOS não autoriza complementação fictícia. Escolha outro modo explicitamente.");
        if (request.DataMode == WorldDataMode.REAL_CLUBS_SIMULATED && request.AllowFictionalTeams)
            errors.Add("CLUBES REAIS + ELENCOS SIMULADOS permite somente clubes reais verificados.");
        var candidates = selection.Clubs.Where(c => c.Gender == request.Gender && c.Season == request.Season && request.Countries.Contains(c.CountryCode))
            .Where(c => Verified(c) || request.DataMode == WorldDataMode.FREE_WORLD &&
                (Fictional(c) && request.AllowFictionalTeams || c.Verification?.Status == CatalogClubVerificationStatus.REAL_CLUB_HISTORICAL && request.IncludeHistoricalClubs ||
                 c.Verification?.Status == CatalogClubVerificationStatus.REAL_CLUB_UNVERIFIED && request.IncludeUnverifiedClubs)).ToList();
        var prepared = candidates.Select(c => {
            var current = CurrentPlayers(c, asOf);
            var reference = (c.ReferencePlayers ?? []).Where(p=>p.IdentityOrigin==CatalogDataOrigin.Real).Concat(c.Players.Where(p => p.IdentityOrigin == CatalogDataOrigin.Real && !Current(p,c,asOf)))
                .DistinctBy(p => p.CanonicalPlayerId ?? $"{p.Source}:{p.PlayerExternalId}")
                .Select(p => p with { PlayerOrigin = WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE }).ToList();
            return c with { IsFictional = Fictional(c), Players = current, ReferencePlayers = reference,
                RosterAsOf = asOf, SourceRanking = c.SourceRanking ?? c.RankingInfo };
        }).ToList();
        if (request.DataMode == WorldDataMode.REAL_COMPLETE) prepared = prepared.Where(c => Complete(c.Players)).ToList();
        // Rank positions from disconnected universes are not a world ranking.
        var sporting = prepared.Where(c => Sporting(c.SourceRanking)).ToList();
        bool comparable = sporting.Select(c => c.SourceRanking!.ComparableGroup).Distinct().Count() <= 1;
        var ranked = comparable ? sporting.OrderBy(c => c.SourceRanking!.Rank).ThenBy(c => Key(request.Seed,c.Key)).ToList() : [];
        var rankedKeys = ranked.Select(c => c.Key).ToHashSet();
        var provisional = prepared.Where(c => !rankedKeys.Contains(c.Key)).OrderBy(c => Key(request.Seed,c.Key)).ToList();
        if (provisional.Count > 0) warnings.Add("Classificação provisória de baixa confiança: desempate técnico por seed, sem ordem por ID apresentada como ranking esportivo.");
        if (sporting.Any(c => (c.SourceRanking!.Confidence ?? 0) < .5 || c.SourceRanking.Method?.Contains("disconnected") == true))
            warnings.Add("Elo estimado provisório: resultados ou comparação entre ligas insuficientes para alta confiança.");
        return ranked.Concat(provisional).Select((c,i) => c with { Rank = i+1,
            RankingInfo = rankedKeys.Contains(c.Key) ? c.SourceRanking! with { Rank = i+1 } : new(c.ExternalId,c.Provider,c.Gender,c.Season,i+1,
                "Desempate técnico por seed",c.References.Select(r=>r.UpdatedAt).DefaultIfEmpty(DateTime.UnixEpoch).Max(),null,
                "ProvisionalSeededOrder-v1",0,$"technical-seed:{request.Seed}",CatalogDataOrigin.Estimated) }).ToList();
    }
    public static List<EligibleCatalogClub> Calibrate(List<EligibleCatalogClub> clubs,int seed)
    {
        var order=clubs.ToDictionary(c=>c.Key,c=>c.Rank);
        var calibrated=InitialPlayerStrength.Apply(clubs.Select(c=>c with { Rank=Sporting(c.SourceRanking)?c.SourceRanking!.Rank:null }).ToList(),seed);
        return calibrated.Select(c=>c with {Rank=order[c.Key]}).ToList();
    }
    public static EligibleCatalogClub Prepare(EligibleCatalogClub club,int seed,bool consent)
    {
        var selected=InitialRosterPolicy.Prepare(club,seed,false).Players;
        if(!consent || Complete(selected))return club with {Players=selected};
        int index=0;
        var raw=club.SourceRanking ?? club.RankingInfo;
        double level=26,confidence=0;
        if(Sporting(raw) && raw!.Score is double score && raw.Method?.Contains("elo",StringComparison.OrdinalIgnoreCase)==true)
        {level=Math.Clamp(26+(score-1500)/35,14,44);confidence=raw.Confidence??0;}
        else if(Sporting(raw) && raw!.Confidence>=.5)
        {level=Math.Clamp(44-6*Math.Log10(Math.Max(1,raw.Rank)),16,44);confidence=raw.Confidence??0;}
        void Add(string position)
        {
            if(selected.Count==24)
            {
                var removable=selected.Where(p=>p.Position!=position && selected.Count(x=>x.Position==p.Position)>1)
                    .OrderBy(p=>p.PlayerOrigin==WorldPlayerOrigin.FICTIONAL_GENERATED?0:1).ThenBy(p=>p.Strength).ThenBy(p=>p.PlayerExternalId,StringComparer.Ordinal).FirstOrDefault();
                if(removable==null)return;selected.Remove(removable);
            }
            int n=++index;string identity=$"generated013c:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(club.Key)))[..16]}:{seed}:{n}";
            double variation=(Key(seed,$"{club.Key}:{position}:{n}")/(double)ulong.MaxValue*2-1)*7;
            int force=Math.Clamp((int)Math.Round(level+variation+(position=="M"?1:0),MidpointRounding.AwayFromZero),1,50);
            selected.Add(new(identity,club.ExternalId,club.Provider,club.Gender,club.Season,$"Atleta fictício {club.ShortName}-{n:00}",position,
                force,"generated013c",DateTime.UnixEpoch,identity,CatalogDataOrigin.Generated,
                new("generated-club-level-v1",CatalogDataOrigin.Generated,confidence,identity,.5,null),
                PlayerOrigin:WorldPlayerOrigin.FICTIONAL_GENERATED));
        }
        foreach(var (position,minimum) in new[]{("G",2),("D",4),("M",4),("A",2)})
            while(selected.Count(p=>p.Position==position)<minimum)Add(position);
        string[] cycle=["D","M","A","D","M","A"];
        for(int i=0;selected.Count<18;i++)Add(cycle[i%cycle.Length]);
        return club with {Players=selected.OrderBy(p=>p.Position=="G"?0:p.Position=="D"?1:p.Position=="M"?2:3)
            .ThenByDescending(p=>p.Strength).ThenBy(p=>p.PlayerExternalId,StringComparer.Ordinal).ToList()};
    }
    public static WorldDataSummary Summary(WorldCreatorRequest request,CatalogSelection selection,List<WorldPreviewClub> assigned,DateOnly asOf)
    {
        var compatible=selection.Clubs.Where(c=>c.Gender==request.Gender&&c.Season==request.Season&&request.Countries.Contains(c.CountryCode)).ToList();
        var verified=compatible.Where(Verified).ToList();int complete=verified.Count(c=>Complete(CurrentPlayers(c,asOf)));
        int eligible=request.DataMode==WorldDataMode.REAL_COMPLETE?complete:verified.Count;
        return new(verified.Count,verified.Count(c=>c.Verification!.CurrentSeasonConfirmed),complete,verified.Count-complete,
            Math.Max(0,128-eligible),assigned.Count(c=>Verified(c.Club)),assigned.Count(c=>Fictional(c.Club)),
            assigned.Sum(c=>c.Club.Players.Count(p=>p.PlayerOrigin==WorldPlayerOrigin.REAL_VERIFIED_CURRENT)),
            assigned.Sum(c=>c.Club.Players.Count(p=>p.PlayerOrigin==WorldPlayerOrigin.FICTIONAL_GENERATED)),
            assigned.Count(c=>!compatible.Any(s=>s.Key==c.Club.Key&&Complete(CurrentPlayers(s,asOf)))),
            assigned.Count(c=>!Verified(c.Club)&&!Fictional(c.Club)));
    }
}