using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class OfflineSurplusV16
{
    public const string ClubId = "v16-surplus-fictional";
    public static WorldPreviewClub Build(List<WorldPreviewClub> realClubs, int seed,
        IReadOnlyList<OfflineRetentionEvidence>? research = null)
    {
        var selectedIds = realClubs.SelectMany(c => c.Club.Players).Select(p => p.PlayerExternalId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var all = realClubs.SelectMany(c => c.Club.ReferencePlayers ?? []).ToList();
        if (all.Select(p => p.PlayerExternalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != all.Count)
            throw new ArgumentException("Excedentes: IDs de atletas ambíguos entre clubes/anos. Resolva a identidade antes de criar.");
        var original = realClubs.SelectMany(c => c.Club.OfflineSource!.Definition.Players!).ToDictionary(p => p.Id!);
        var evidence = (research ?? []).ToDictionary(e => (e.ClubKey, e.PlayerId));
        var cuts = realClubs.OrderBy(c => c.Club.Rank).SelectMany(c => (c.Club.ReferencePlayers ?? [])
            .Where(p => !selectedIds.Contains(p.PlayerExternalId))
            .Select(p => (Player: p, Evidence: evidence.GetValueOrDefault((c.Club.Key, p.PlayerExternalId))))
            .OrderByDescending(p => original[p.Player.PlayerExternalId].IsStar == true)
            .ThenByDescending(p => p.Evidence?.IsProdigy == true)
            .ThenByDescending(p => p.Evidence?.ImportantStarter == true)
            .ThenByDescending(p => p.Evidence?.Minutes ?? -1)
            .ThenByDescending(p => p.Player.Strength)
            .ThenBy(p => OfflineRosterV16.Draw(seed, c.Club.Key, p.Player.PlayerExternalId))
            .ThenBy(p => p.Player.PlayerExternalId, StringComparer.Ordinal)
            .Select(p => p.Player)).ToList();
        if (cuts.Count(p => p.Position == "G") < 3 || cuts.Count(p => p.Position != "G") < 20)
            throw new ArgumentException("Excedentes insuficientes: exige 3 G reais e 20 de linha cortados. Desligue o clube de excedentes ou revise os elencos; nenhum atleta será inventado.");
        var players = cuts.Where(p => p.Position == "G").Take(3).ToList();
        foreach (var (position, count) in new[] { ("D",7), ("M",7), ("A",6) })
            players.AddRange(cuts.Where(p => p.Position == position).Take(count));
        var used = players.Select(p => p.PlayerExternalId).ToHashSet();
        players.AddRange(cuts.Where(p => p.Position != "G" && !used.Contains(p.PlayerExternalId)).Take(23-players.Count));
        if (players.Count(p => p.Position == "D") < 4 || players.Count(p => p.Position == "M") < 4 || players.Count(p => p.Position == "A") < 2)
            throw new ArgumentException("Excedentes sem equilíbrio para 4-4-2; desligue o clube especial ou revise os candidatos.");
        var first = realClubs[0].Club;
        var source = first.OfflineSource!;
        var definition = new OfflineClub("1.1",ClubId,source.CountryCode,source.Year,
            "Excedentes — clube fictício V1.6","Excedentes (fictício)","EXC",["#444444","#FFD700"],null,
            new("v16-surplus-coach-fictional","Treinador virtual dos Excedentes",source.CountryCode,new(["4-4-2"],[])),
            players.Select(p => original[p.PlayerExternalId]).ToList(),"READY",InitialDivision:5);
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { seed, players })));
        var origin = new OfflineClubSource("offline-v16-surplus",source.CountryCode,source.CountryName,source.Year,hash,definition,
            V11:new(new("1.1",ClubId,definition.Name,definition.ShortName,definition.Abbreviation,definition.Colors,null),1,null,128));
        var club = OfflineWorldFactory.Eligible(origin,first.Gender) with {
            Players=players,ReferencePlayers=players,Rank=128,IsFictional=true,
            CoverageWarnings=["Clube especial fictício. Força coletiva 1 por regra de gameplay, sem dado Opta. Atletas reais cortados; identidades originais preservadas."]
        };
        if (realClubs.Any(c => c.Club.Key == club.Key || c.Club.ExternalId == ClubId))
            throw new ArgumentException("Identidade do clube especial conflita com catálogo.");
        return new(5,club);
    }
}
