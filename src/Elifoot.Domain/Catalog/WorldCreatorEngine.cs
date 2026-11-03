using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class WorldCreatorEngine
{
    public static string ClubKey(string country, FootballGender gender, string provider, string externalId)
        => $"{country}:{gender}:{provider}:{externalId}";

    public static WorldCreatorPreview Preview(WorldCreatorRequest request, CatalogSelection selection)
    {
        if (request.Assignments == null || request.Countries == null || request.Season == null) throw new ArgumentException("Configuração incompleta.");
        var errors = new List<string>();
        var warnings = selection.Warnings.ToList();
        if (!Enum.IsDefined(request.Gender) || !Enum.IsDefined(request.Mode) || !Enum.IsDefined(request.DistrictMode))
            errors.Add("Categoria ou modo desconhecido.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 160) errors.Add("Informe um nome de até 160 caracteres.");
        if (request.Countries.Count == 0 || request.UseWorldCatalogRules && request.Countries.Count>8 || request.Countries.Distinct().Count() != request.Countries.Count)
            errors.Add("Selecione países sem repetição.");
        if (request.MaxHumans is < 1 or > 8) errors.Add("Selecione de 1 a 8 treinadores.");
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var allowed = selection.Clubs.Where(c => c.Verification == null ||
            (c.Verification.Status != CatalogClubVerificationStatus.REAL_CLUB_HISTORICAL || request.IncludeHistoricalClubs) &&
            (c.Verification.Status != CatalogClubVerificationStatus.REAL_CLUB_UNVERIFIED || request.IncludeUnverifiedClubs)).ToList();
        if (allowed.Count < selection.Clubs.Count) warnings.Add("Clubes históricos ou sem verificação foram excluídos; a inclusão exige escolha explícita.");
        if (request.DataMode != null) allowed = PlayableWorldPolicy.Select(request,selection,asOf,errors,warnings);
        var calibrated = request.DataMode == null ? InitialPlayerStrength.Apply(allowed, request.Seed) : PlayableWorldPolicy.Calibrate(allowed,request.Seed);
        var prepared = request.DataMode != null ? calibrated.Select(c => PlayableWorldPolicy.Prepare(c,request.Seed,request.DataMode != WorldDataMode.REAL_COMPLETE && request.AllowFictionalPlayers)).ToList()
            : calibrated.Select(c => InitialRosterPolicy.IsRealImport(c)
            ? c with { Players = InitialRosterPolicy.Prepare(c, request.Seed, request.AllowFictionalPlayers).Players } : c).ToList();
        var eligible = prepared.Where(c => c.Gender == request.Gender && c.Season == request.Season &&
            request.Countries.Contains(c.CountryCode)).ToDictionary(c => c.Key);
        var ordered = eligible.Values.OrderBy(c => c.Rank ?? int.MaxValue).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();
        if(request.UseWorldCatalogRules) {
            if(request.GameModeId!="CLASSIC_SYNC") errors.Add("Somente o modo clássico sincronizado está implementado.");
            if(request.EliteFullSquadRankCutoff is <0 or >1000) errors.Add("Limite de elite inválido.");
            if(request.FictionalOnly) {
                if(!request.AllowFictionalTeams || !request.AllowFictionalPlayers)errors.Add("Mundo fictício exige autorização de clubes e atletas gerados.");
                ordered=[];eligible.Clear();
            } else {
                int real=ordered.Count(c=>!PlayableWorldPolicy.Fictional(c));
                int minimum=request.AllowFictionalTeams?32:128;
                if(real<minimum)errors.Add($"Há {real} clubes reais elegíveis; faltam {minimum-real} para o mínimo de {minimum}.");
            }
            ordered=WorldCatalogRules016B.Order(ordered);
            ordered=ordered.Select((c,i)=>WorldCatalogRules016B.Prepare(c,i,request.Seed,request.AllowFictionalPlayers,request.EliteFullSquadRankCutoff)).ToList();
            eligible=ordered.ToDictionary(c=>c.Key);
        }
        if (ordered.Any(c => c.Rank == null)) warnings.Add("Ranking ausente: clubes sem classificação ficam ao final, ordenados por identificador. Ranking só define a montagem inicial.");
        if (ordered.Count < 128) warnings.Add($"Há {ordered.Count} clubes disponíveis; faltam {128 - ordered.Count} para 128.");
        var assigned = new List<WorldPreviewClub>();
        var used = new HashSet<string>();
        foreach (var a in request.Assignments)
        {
            if (a.Division is < 1 or > 5) { errors.Add("Divisão inválida."); continue; }
            if (request.Mode == WorldAssemblyMode.Automatic && a.Division != 5)
            { errors.Add("Escolhas de divisões exigem modo personalizado."); continue; }
            if (request.Mode == WorldAssemblyMode.Automatic && a.Division == 5 && ordered.Take(32).Any(c => c.Key == a.ClubKey))
            { errors.Add("Clube reservado às divisões pelo ranking automático; altere para personalizado para movê-lo."); continue; }
            string assignmentKey = eligible.ContainsKey(a.ClubKey) ? a.ClubKey : eligible.Values.FirstOrDefault(c =>
                c.Aliases?.Any(alias => ClubKey(c.CountryCode, c.Gender, alias.Provider, alias.ExternalId) == a.ClubKey) == true)?.Key ?? a.ClubKey;
            if (!eligible.TryGetValue(assignmentKey, out var club)) { errors.Add($"Clube fora da seleção: {a.ClubKey}."); continue; }
            if (!used.Add(club.Key)) { errors.Add($"Clube repetido: {club.Name}."); continue; }
            assigned.Add(new(a.Division, club));
        }
        int fictionalIndex = 0;
        EligibleCatalogClub Fictional()
        {
            var country = request.Countries.FirstOrDefault() ?? "XX";
            string id = $"fictional-{++fictionalIndex}";
            if(request.DataMode!=null)
            {
                country=request.Countries.Count>0?request.Countries[(fictionalIndex-1)%request.Countries.Count]:"XX";
                id=$"fictional013c-{request.Seed}-{fictionalIndex}";
                var club=new EligibleCatalogClub(ClubKey(country,request.Gender,"fictional013c",id),id,"fictional013c",$"Equipe fictícia {country}-{fictionalIndex:000}",
                    $"F{fictionalIndex:000}",country,selection.Clubs.FirstOrDefault(c=>c.CountryCode==country)?.CountryName??country,request.Gender,"Fictícia",request.Season,
                    "#006400","#FFFFFF",null,[],[],true,Verification:new(CatalogClubVerificationStatus.FICTIONAL_CLUB,"Complemento fictício autorizado",DateTime.UnixEpoch));
                return PlayableWorldPolicy.Prepare(club,request.Seed,request.AllowFictionalPlayers);
            }
            var positions = new[] { "G", "G", "D", "D", "D", "D", "D", "D", "M", "M", "M", "M", "M", "M", "A", "A", "A", "A" };
            var players = positions.Select((p, i) => new CatalogAthlete($"{id}-p{i}", id, "fictional", request.Gender,
                request.Season, $"Atleta fictício {fictionalIndex}-{i + 1}", p, 15 + ((i + fictionalIndex) % 16),
                "Fictício autorizado", DateTime.UnixEpoch)).ToList();
            return new(ClubKey(country, request.Gender, "fictional", id), id, "fictional", $"Equipe fictícia {fictionalIndex:000}",
                $"F{fictionalIndex:000}", country, country, request.Gender, "Fictícia", request.Season,
                "#006400", "#FFFFFF", null, players, [], true, Verification: new(CatalogClubVerificationStatus.FICTIONAL_CLUB, "Complemento fictício autorizado", DateTime.UnixEpoch));
        }
        void Fill(int division, IEnumerable<EligibleCatalogClub> candidates)
        {
            int capacity = division == 5 ? 96 : 8;
            int count = assigned.Count(c => c.Division == division);
            if (count > capacity) { errors.Add($"Divisão {division}: máximo {capacity} clubes."); return; }
            foreach (var club in candidates)
            {
                if (count >= capacity) break;
                if (!used.Add(club.Key)) continue;
                assigned.Add(new(division, club)); count++;
            }
            while (count < capacity && request.AllowFictionalTeams && (!request.UseWorldCatalogRules || division==5 || request.FictionalOnly) && (request.DataMode == null || request.DataMode == WorldDataMode.FREE_WORLD && request.AllowFictionalPlayers))
            {
                var club = Fictional();
                if(request.UseWorldCatalogRules)club=WorldCatalogRules016B.Prepare(club,assigned.Count,request.Seed,request.AllowFictionalPlayers,0);
                used.Add(club.Key); assigned.Add(new(division, club)); count++;
            }
        }
        for (int d = 1; d <= 4; d++)
        {
            if (request.Mode == WorldAssemblyMode.Automatic || request.FillRemainingDivisions) Fill(d, ordered);
            if (assigned.Count(c => c.Division == d) != 8) errors.Add($"Divisão {d} precisa de exatamente 8 clubes.");
        }
        if (request.DistrictMode != DistrictFillMode.Manual)
        {
            var remaining = ordered.Where(c => !used.Contains(c.Key)).ToList();
            if (request.DistrictMode == DistrictFillMode.Random)
            {
                var rng = new DeterministicRng(request.Seed);
                for (int i = remaining.Count - 1; i > 0; i--)
                { int j = rng.NextInt(0, i + 1); (remaining[i], remaining[j]) = (remaining[j], remaining[i]); }
            }
            Fill(5, remaining);
        }
        if (assigned.Count(c => c.Division == 5) != 96) errors.Add("Distritais precisam de exatamente 96 clubes.");
        foreach (var item in assigned)
        {
            var c = item.Club;
            var p = c.Players;
            if (InitialRosterPolicy.IsRealImport(c)) errors.AddRange(InitialRosterPolicy.Prepare(c, request.Seed, false).Issues);
            if (p.Count is < 11 or > 24 || !p.Any(x => x.Position == "G") || p.Count(x => x.Position != "G") < 10)
                errors.Add($"Elenco incompleto ou inválido: {c.Name}; exige 11–24 atletas, 1 G e 10 de linha.");
            if (p.Any(x => x.Gender != request.Gender || x.Season != request.Season || x.Strength is < 1 or > 50))
                errors.Add($"Categoria, temporada ou força inválida no elenco de {c.Name}.");
        }
        var allPlayers = assigned.SelectMany(c => c.Club.Players.Select(p => p.CanonicalPlayerId ?? $"{p.Source}:{p.PlayerExternalId}")).ToList();
        if (allPlayers.Distinct().Count() != allPlayers.Count) errors.Add("Jogadores duplicados entre elencos.");
        if(request.UseWorldCatalogRules)foreach(var c in assigned) {
            if(c.Club.Players.Count!=c.Club.InitialProfile?.TargetInitialSquadSize)
                errors.Add($"{c.Club.Name}: {c.Club.Players.Count} atletas válidos; alvo {c.Club.InitialProfile?.TargetInitialSquadSize}. Complementação requer autorização.");
            if(c.Club.Players.Count<11 || !c.Club.Players.Any(p=>p.Position=="G") || c.Club.Players.Count(p=>p.Position!="G")<10)
                errors.Add($"{c.Club.Name}: elenco sem o mínimo de 1 goleiro e 10 de linha.");
        }
        foreach (var conflict in assigned.SelectMany(c => c.Club.IdentityConflicts ?? []).Distinct())
            if (assigned.Count(c => c.Club.IdentityConflicts?.Contains(conflict) == true) > 1) errors.Add("Identidades de equipes precisam de revisão antes de usar ambas no mesmo mundo.");
        if (assigned.SelectMany(c => c.Club.Players).Any(p => p.IdentityOrigin == CatalogDataOrigin.Generated)) warnings.Add("Complemento de atletas fictícios autorizado; não são jogadores reais importados.");
        if (assigned.Any(c => c.Club.IsFictional)) warnings.Add("Complemento fictício autorizado nesta configuração.");
        var result = InitialSquadStrength017B.Apply(assigned.OrderBy(c => c.Division), request.Seed);
        var usedReferences = result.SelectMany(c => c.Club.References).DistinctBy(r => r.SnapshotId).ToList();
        string hash = SnapshotHash(request, result, usedReferences);
        return new(Guid.NewGuid(), request.Name, request.Gender, request.Seed, result,
            Math.Max(0, 128 - result.Count), errors.Distinct().ToList(), warnings.Distinct().ToList(),
            usedReferences, DateTime.UtcNow, hash,request.DataMode,request.DataMode==null?null:asOf,
            request.DataMode==null?null:PlayableWorldPolicy.Summary(request,selection,result,asOf));
    }

    private static string SnapshotHash(WorldCreatorRequest request, List<WorldPreviewClub> result, List<CatalogReference> references) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request, result, References = references })));

    public static GameWorld Create(WorldCreatorRequest request, WorldCreatorPreview preview)
    {
        if (preview.DataMode != request.DataMode) throw new ArgumentException("Modo de dados difere da prévia persistida.");
        if (preview.Errors.Count > 0 || preview.PendingSlots != 0 || preview.Clubs.Count != 128)
            throw new ArgumentException("Resolva as vagas e os elencos antes de confirmar.");
        if (preview.Clubs.Any(c => c.Division is >= 1 and <= 4 && c.InitialSquadReference == null))
        {
            var clubs = InitialSquadStrength017B.Apply(preview.Clubs, request.Seed);
            preview = preview with { Clubs = clubs, DataHash = SnapshotHash(request, clubs, preview.References) };
        }
        if(request.DataMode!=null)
        {
            if(preview.Seed!=request.Seed || preview.Gender!=request.Gender) throw new ArgumentException("Categoria ou seed difere da prévia.");
            foreach(var entry in preview.Clubs)
            {
                if(PlayableWorldPolicy.Fictional(entry.Club) && (request.DataMode!=WorldDataMode.FREE_WORLD || !request.AllowFictionalTeams))
                    throw new ArgumentException("Clube fictício sem autorização.");
                foreach(var player in entry.Club.Players)
                {
                    if(player.PlayerOrigin==WorldPlayerOrigin.FICTIONAL_GENERATED)
                    {if(request.DataMode==WorldDataMode.REAL_COMPLETE || !request.AllowFictionalPlayers)throw new ArgumentException("Atleta fictício sem autorização.");}
                    else if(!(player.PlayerOrigin==WorldPlayerOrigin.REAL_VERIFIED_CURRENT && PlayableWorldPolicy.Current(player,entry.Club,preview.DataAsOf!.Value)) &&
                        !(request.AllowProviderReportedPlayers && request.DataMode!=WorldDataMode.REAL_COMPLETE && PlayableWorldPolicy.ProviderReported(player,entry.Club,preview.DataAsOf!.Value)))
                        throw new ArgumentException("Atleta sem vínculo atual verificado no elenco inicial.");
                }
            }
        }
        var world = WorldSeedFactory.CreateSeededWorld(request.Name, request.Seed, 128, request.MaxHumans,
            forceEvolutionMode: ForceEvolutionMode.ClassicAdapted);
        var templates = world.Players.Where(p => p.ClubId != null).GroupBy(p => p.ClubId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        world.Players.Clear();
        world.Auctions.Clear(); // No unrelated synthetic free agents enter an imported category.
        for (int i = 0; i < 128; i++)
        {
            var entry = preview.Clubs[i];
            var club = world.Clubs[i];
            club.ClubInitialPower=entry.Club.InitialProfile?.ClubInitialPower; club.InitialSquadSize=entry.Club.InitialProfile?.TargetInitialSquadSize;
            club.InitialSquadReference = entry.InitialSquadReference;
            club.Name = entry.Club.Name; club.ShortName = entry.Club.ShortName;
            club.ColorPalette = entry.Club.ColorPalette;
            club.Country = entry.Club.CountryName; club.PrimaryColorHex = entry.Club.PrimaryColor;
            club.SecondaryColorHex = entry.Club.SecondaryColor; club.DivisionTier = (DivisionTier)entry.Division;
            club.Stadium.Name = $"Estádio de {club.Name}";
            var squad = new List<Player>();
            for (int j = 0; j < entry.Club.Players.Count; j++)
            {
                var source = entry.Club.Players[j];
                var template = templates[club.Id][j % 18];
                var player = new Player {
                    Id = WorldSeedFactory.CreateDeterministicGuid(request.Seed, 4, i * 100 + j + 1),
                    GameWorldId = world.Id, ClubId = club.Id, Name = source.PlayerName,
                    Position = Enum.Parse<PlayerPosition>(source.Position), Strength = source.Strength,
                    Behavior = template.Behavior, IsStar = template.IsStar,
                    ContractRoundsRemaining = template.ContractRoundsRemaining,
                    SalaryPerRound = MarketAndEconomyEngine.CalculateFairSalaryPerRound(source.Strength, template.IsStar, Enum.Parse<PlayerPosition>(source.Position))
                };
                if(source.PlayerOrigin is WorldPlayerOrigin origin)
                    player.CatalogIdentity=new(origin,source.Source,source.PlayerExternalId,source.CanonicalPlayerId??$"{source.Source}:{source.PlayerExternalId}",
                        entry.Club.Key,entry.Club.CountryCode,request.Gender,request.Season,origin==WorldPlayerOrigin.FICTIONAL_GENERATED?preview.CreatedAt:source.UpdatedAt,preview.DataAsOf??DateOnly.FromDateTime(preview.CreatedAt),
                        entry.Club.References.ToList(),source.MembershipEvidence,origin==WorldPlayerOrigin.FICTIONAL_GENERATED?request.Seed:null,source.StrengthEstimate);
                player.MarketPrice = MarketAndEconomyEngine.CalculateFairValue(player);
                squad.Add(player); world.Players.Add(player);
            }
            club.PreferredStarterIds.Clear(); club.PreferredBenchIds.Clear();
            var manager = world.AiManagers.Single(m => m.Id == club.AiManagerId);
            AiManagerBrain.PlanAndSelectLineup(manager, club, squad, null, null, true, request.Seed, 1, 1, world.AllowClassic505);
        }
        world.ClassicForce.CalendarSeason = 0;
        ClassicForceEvolutionEngine.InitializeSeason(world);
        world.SetCatalogOrigin(new(request.Gender, request.Season, request.Countries.ToList(), request.Seed,
            preview.CreatedAt, preview.DataHash, preview.Clubs, preview.References,request.DataMode,request.DataMode==null?null:request.Seed,preview.DataAsOf,preview.DataSummary,
            preview.Clubs.Select(c=>c.Club.SourceRanking??c.Club.RankingInfo).Where(r=>r!=null).Select(r=>r!).ToList()));
        return world;
    }
}
