using System.Numerics;
using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Read-only sporting telemetry. Never consumes RNG or changes match decisions.</summary>
public static class PlayerStatisticsEngine
{
    public const decimal TopScorerPrize = 3_000_000m;

    public static void CaptureLineup(MatchFixture match, IReadOnlyDictionary<Guid, Player> players)
    {
        ClassicForceEvolutionEngine.CaptureRoster(match, players);
        if (match.StatisticsLineupCaptured) return;
        void Side(Guid club, IEnumerable<Guid> active, IEnumerable<Guid> bench)
        {
            foreach (var id in active.Distinct())
                if (players.TryGetValue(id, out var p)) match.StatisticsInitialLineup.Add(new(id, club, p.Position, p.Strength, true));
            foreach (var id in bench.Distinct())
                if (players.TryGetValue(id, out var p) && !match.StatisticsInitialLineup.Any(x => x.PlayerId == id))
                    match.StatisticsInitialLineup.Add(new(id, club, p.Position, p.Strength, false));
        }
        Side(match.HomeClubId, match.HomeActiveStarterIds, match.HomeBenchIds);
        Side(match.AwayClubId, match.AwayActiveStarterIds, match.AwayBenchIds);
        match.StatisticsLineupCaptured = match.StatisticsInitialLineup.Count(x => x.IsStarter && x.ClubId == match.HomeClubId) is >=1 and <=11
            && match.StatisticsInitialLineup.Count(x => x.IsStarter && x.ClubId == match.AwayClubId) is >=1 and <=11;
        if (!match.StatisticsLineupCaptured) match.StatisticsInitialLineup.Clear();
    }

    public static void EnsureMigrated(GameWorld world)
    {
        if (world.PlayerStatisticsVersion >= 9) return;
        world.PlayerStatisticsVersion = 9;
        foreach (var season in world.SeasonHistories.Select(s => s.SeasonNumber).Concat(Enumerable.Range(1, Math.Max(0, world.SeasonNumber - 1))))
            world.ClosedPlayerAwardSeasons.Add(season); // No retroactive payouts from incomplete legacy summaries.
        var players = world.Players.ToDictionary(p => p.Id);
        foreach (var match in world.Fixtures.Where(f => f.IsFinished && f.CurrentMinute > 0).OrderBy(f => f.SeasonNumber).ThenBy(f => f.RoundNumber))
        {
            if (!match.StatisticsLineupCaptured)
                Warn(world, "Legado: escalações iniciais reconstituídas somente quando as listas e eventos permitem; posição e força sem snapshot anterior usam o cadastro disponível na migração.");
            RecoverLineup(match, players);
            ApplyOne(world, match, players);
        }
        if (world.SeasonNumber > 2)
            Warn(world, "Legado: fixtures antigos podem ter sido removidos; carreira anterior aos eventos disponíveis não foi inventada.");
        if (world.Players.Sum(p => (long)p.CareerGoals) > world.PlayerStatistics.Sum(s => (long)s.GoalsScored))
            Warn(world, "Legado: contadores anteriores contêm gols sem eventos recuperáveis; não foram distribuídos artificialmente por competição.");
    }

    private static void RecoverLineup(MatchFixture m, IReadOnlyDictionary<Guid, Player> players)
    {
        if (m.StatisticsLineupCaptured) return;
        var home = m.HomeActiveStarterIds.Concat(m.HomeSubstitutedOutIds).Concat(m.HomeSentOffIds).Except(m.HomeSubstitutedInIds).Distinct().ToList();
        var away = m.AwayActiveStarterIds.Concat(m.AwaySubstitutedOutIds).Concat(m.AwaySentOffIds).Except(m.AwaySubstitutedInIds).Distinct().ToList();
        if (home.Count != 11 || away.Count != 11 || home.Concat(away).Any(id => !players.ContainsKey(id))) return;
        // Substitution timing must be present, otherwise final lists alone cannot establish minutes/keepers.
        var subs = m.Events.Where(e => e.EventType == MatchEventType.Substitution).ToList();
        if (m.HomeSubstitutedInIds.Concat(m.AwaySubstitutedInIds).Any(id => !subs.Any(e => e.PrimaryPlayerId == id))) return;
        if (m.HomeSentOffIds.Concat(m.AwaySentOffIds).Any(id => !m.Events.Any(e => e.PrimaryPlayerId == id && e.EventType is MatchEventType.DirectRedCard or MatchEventType.SecondYellowRedCard))) return;
        foreach (var (club, starters, bench) in new[] { (m.HomeClubId, home, m.HomeBenchIds.Concat(m.HomeSubstitutedInIds)), (m.AwayClubId, away, m.AwayBenchIds.Concat(m.AwaySubstitutedInIds)) })
        {
            foreach (var id in starters) { var p = players[id]; m.StatisticsInitialLineup.Add(new(id, club, p.Position, p.Strength, true)); }
            foreach (var id in bench.Distinct())
                if (players.TryGetValue(id, out var p) && !starters.Contains(id)) m.StatisticsInitialLineup.Add(new(id, club, p.Position, p.Strength, false));
        }
        m.StatisticsLineupCaptured = true;
    }

    public static void ApplyFinishedMatches(GameWorld world, IEnumerable<MatchFixture> matches)
    {
        EnsureMigrated(world);
        var pending = matches.Where(m => m.IsFinished && m.CurrentMinute > 0 && !world.AppliedPlayerStatisticsMatches.Contains(m.Id)).ToList();
        if (pending.Count == 0) return;
        var players = world.Players.ToDictionary(p => p.Id);
        foreach (var m in pending) { RecoverLineup(m, players); ApplyOne(world, m, players); }
    }

    private sealed class Appearance(PlayerLineupSnapshot snapshot)
    {
        public PlayerLineupSnapshot Snapshot { get; } = snapshot;
        public bool Played { get; set; }
        public int Minutes, Goals, Assists, Conceded, Yellow, Red;
    }

    private static void ApplyOne(GameWorld w, MatchFixture m, IReadOnlyDictionary<Guid, Player> players)
    {
        if (w.AppliedPlayerStatisticsMatches.Contains(m.Id)) return;
        // Build the entire per-player delta before touching any aggregate.
        var appearances = m.StatisticsInitialLineup.DistinctBy(x => x.PlayerId).ToDictionary(x => x.PlayerId, x => new Appearance(x) { Played = x.IsStarter });
        bool complete = m.StatisticsLineupCaptured;
        bool goalsComplete = true;
        bool assistsComplete = true;
        var active = new Dictionary<Guid, List<Guid>> {
            [m.HomeClubId] = m.StatisticsInitialLineup.Where(x => x.IsStarter && x.ClubId == m.HomeClubId).Select(x => x.PlayerId).ToList(),
            [m.AwayClubId] = m.StatisticsInitialLineup.Where(x => x.IsStarter && x.ClubId == m.AwayClubId).Select(x => x.PlayerId).ToList()
        };
        int last = 0, end = Math.Clamp(m.CurrentMinute, 0, 120);
        void Advance(int minute)
        {
            int elapsed = Math.Max(0, minute - last);
            foreach (var id in active.Values.SelectMany(x => x)) appearances[id].Minutes += elapsed;
            last = minute;
        }
        foreach (var e in m.Events.Select((e,i) => (Event:e,Index:i)).OrderBy(x => x.Event.Minute).ThenBy(x => x.Index).Select(x => x.Event))
        {
            if (e.Minute > end || e.Minute < 0 || !active.ContainsKey(e.ClubId)) continue;
            Advance(e.Minute);
            if (!complete && e.PrimaryPlayerId is Guid known && players.TryGetValue(known, out var p) && !appearances.ContainsKey(known)
                && e.EventType is MatchEventType.Goal or MatchEventType.YellowCard or MatchEventType.SecondYellowRedCard or MatchEventType.DirectRedCard or MatchEventType.Substitution)
                appearances.Add(known, new(new(known, e.IsOwnGoal ? (e.ClubId == m.HomeClubId ? m.AwayClubId : m.HomeClubId) : e.ClubId, p.Position, p.Strength, false)) { Played = true });
            appearances.TryGetValue(e.PrimaryPlayerId ?? Guid.Empty, out var a);
            switch (e.EventType)
            {
                case MatchEventType.Goal:
                    bool validScorer = a != null && a.Snapshot.ClubId == e.ClubId && (!complete || active[e.ClubId].Contains(a.Snapshot.PlayerId));
                    if (!e.IsOwnGoal && validScorer) a!.Goals++;
                    if (!e.HasAssistCoverage) assistsComplete = false;
                    if (!e.IsOwnGoal && e.AssistPlayerId is Guid assistId)
                    {
                        if (assistId != e.PrimaryPlayerId && appearances.TryGetValue(assistId, out var assist) &&
                            assist.Snapshot.ClubId == e.ClubId && active[e.ClubId].Contains(assistId)) assist.Assists++;
                        else assistsComplete = false;
                    }
                    if (!e.IsOwnGoal && !validScorer)
                    {
                        goalsComplete = false;
                        Warn(w, $"Temporada {m.SeasonNumber}: gol oficial sem autor em campo verificável; placar e goleiro preservados, sem crédito individual inventado.");
                    }
                    Guid defending = e.ClubId == m.HomeClubId ? m.AwayClubId : m.HomeClubId;
                    var keeper = active[defending].Select(id => appearances[id]).FirstOrDefault(x => x.Snapshot.Position == PlayerPosition.G)
                        ?? active[defending].Select(id => appearances[id]).OrderByDescending(x => x.Snapshot.Strength).FirstOrDefault();
                    if (keeper != null) keeper.Conceded++;
                    break;
                case MatchEventType.YellowCard:
                    if (a != null) a.Yellow++;
                    break;
                case MatchEventType.SecondYellowRedCard:
                    if (a != null) { a.Yellow++; a.Red++; active[e.ClubId].Remove(a.Snapshot.PlayerId); }
                    break;
                case MatchEventType.DirectRedCard:
                    if (a != null) { a.Red++; active[e.ClubId].Remove(a.Snapshot.PlayerId); }
                    break;
                case MatchEventType.Injury when m.ForceRulesVersion == ClassicForceEvolutionEngine.Version || m.Discipline.KickoffCaptured:
                    if (a != null) active[e.ClubId].Remove(a.Snapshot.PlayerId);
                    break;
                case MatchEventType.Substitution:
                    if (e.SecondaryPlayerId is Guid outgoing) active[e.ClubId].Remove(outgoing);
                    if (a != null) { a.Played = true; if (!active[e.ClubId].Contains(a.Snapshot.PlayerId)) active[e.ClubId].Add(a.Snapshot.PlayerId); }
                    break;
            }
        }
        Advance(end);
        if (!complete) Warn(w, $"Temporada {m.SeasonNumber}: escalação/minutos incompletos em partidas legadas; somente fatos individuais recuperáveis, médias indisponíveis e G/D fora da classificação principal.");
        int eventGoals = m.Events.Count(e => e.EventType == MatchEventType.Goal);
        if (eventGoals != m.HomeGoals + m.AwayGoals)
        {
            complete = false;
            goalsComplete = false;
            Warn(w, $"Temporada {m.SeasonNumber}: placar e eventos de gols divergentes; não foram inventados autores ou gols sofridos.");
        }
        EnsureIndex(w);
        foreach (var a in appearances.Values.Where(x => x.Played))
        {
            var p = a.Snapshot;
            string key = BucketKey(p.PlayerId, m.SeasonNumber, m.CompetitionType, m.DivisionTier, p.ClubId, p.Position);
            string clubName = w.Clubs.FirstOrDefault(c => c.Id == p.ClubId)?.Name ?? string.Empty;
            if (!w.PlayerStatisticsIndex.TryGetValue(key, out var bucket))
            {
                bucket = new() { PlayerId = p.PlayerId, Season = m.SeasonNumber, CompetitionType = m.CompetitionType,
                    Division = m.CompetitionType == CompetitionType.League ? m.DivisionTier : null, ClubId = p.ClubId, ClubName = clubName, Position = p.Position,
                    HasIncompleteAssists = false };
                w.PlayerStatistics.Add(bucket);w.PlayerStatisticsIndex.Add(key, bucket);
            }
            if (!bucket.AppliedMatchIds.Add(m.Id)) continue;
            if (string.IsNullOrWhiteSpace(bucket.ClubName) && !string.IsNullOrWhiteSpace(clubName)) bucket.ClubName = clubName;
            bucket.MatchesPlayed++; bucket.GoalsScored += a.Goals; bucket.GoalsConceded += a.Conceded;
            bucket.Assists += a.Assists;
            bucket.HasIncompleteAssists |= !assistsComplete;
            bucket.YellowCards += a.Yellow; bucket.RedCards += a.Red; bucket.MinutesPlayed += a.Minutes;
            bucket.HasIncompleteAppearances |= !complete;
            bucket.HasIncompleteGoals |= !goalsComplete;
        }
        w.AppliedPlayerStatisticsMatches.Add(m.Id);
    }

    private static string BucketKey(Guid p, int season, CompetitionType competition, DivisionTier? division, Guid club, PlayerPosition position)
        => $"{p}:{season}:{competition}:{(competition == CompetitionType.League ? division : null)}:{club}:{position}";
    private static void EnsureIndex(GameWorld w)
    {
        if (w.PlayerStatisticsIndex.Count == w.PlayerStatistics.Count) return;
        w.PlayerStatisticsIndex.Clear();
        foreach (var s in w.PlayerStatistics) w.PlayerStatisticsIndex[BucketKey(s.PlayerId,s.Season,s.CompetitionType,s.Division,s.ClubId,s.Position)] = s;
    }
    private static void Warn(GameWorld w, string warning) { if (!w.PlayerStatisticsCoverageWarnings.Contains(warning)) w.PlayerStatisticsCoverageWarnings.Add(warning); }

    private sealed record Total(Player Player, int Matches, int Goals, int Conceded, int Yellow, int Red, int Minutes, bool Incomplete,
        int Assists = 0, bool IncompleteAssists = false);
    private static List<Total> Totals(GameWorld w, string scope, int? season, string category = "scorers")
    {
        var source = w.PlayerStatistics.Where(s => season == null || s.Season == season);
        source = scope switch {
            "general" => source,
            "cup" => source.Where(s => s.CompetitionType != CompetitionType.League),
            _ => source.Where(s => s.CompetitionType == CompetitionType.League && (int?)s.Division == int.Parse(scope[^1..]))
        };
        if (category == "goalkeepers") source = source.Where(s => s.Position == PlayerPosition.G);
        if (category == "defenders") source = source.Where(s => s.Position == PlayerPosition.D);
        var players = w.Players.ToDictionary(p => p.Id);
        return source.GroupBy(s => s.PlayerId).Where(g => players.ContainsKey(g.Key)).Select(g => new Total(players[g.Key],
            g.Sum(s => s.MatchesPlayed),g.Sum(s => s.GoalsScored),g.Sum(s => s.GoalsConceded),g.Sum(s => s.YellowCards),g.Sum(s => s.RedCards),g.Sum(s => s.MinutesPlayed),g.Any(s => s.HasIncompleteAppearances),
            g.Sum(s => s.Assists), g.Any(s => s.HasIncompleteAssists))).ToList();
    }
    private static int Ratio(long a, int b, long c, int d) => ((BigInteger)a * Math.Max(1,d)).CompareTo((BigInteger)c * Math.Max(1,b));
    private static int Compare(Total a, Total b, string category)
    {
        int value;
        if (category is "scorers" or "assists") {
            int Count(Total t) => category == "assists" ? t.Assists : t.Goals;
            bool Incomplete(Total t) => t.Incomplete || category == "assists" && t.IncompleteAssists;
            value = Count(b).CompareTo(Count(a)); if (value != 0) return value;
            value = Incomplete(a).CompareTo(Incomplete(b)); if (value != 0) return value;
            value = Ratio(Count(b),b.Matches,Count(a),a.Matches); if(value != 0)return value;
        } else if (category == "goalkeepers") {
            value = Ratio(a.Conceded,a.Matches,b.Conceded,b.Matches); if(value != 0)return value;
            value = b.Matches.CompareTo(a.Matches);if(value != 0)return value;
            value = a.Conceded.CompareTo(b.Conceded);if(value != 0)return value;
        } else {
            value = Ratio(a.Yellow+3L*a.Red,a.Matches,b.Yellow+3L*b.Red,b.Matches);if(value != 0)return value;
            value = Ratio(a.Red,a.Matches,b.Red,b.Matches);if(value != 0)return value;
            value = Ratio(a.Yellow,a.Matches,b.Yellow,b.Matches);if(value != 0)return value;
            value = b.Matches.CompareTo(a.Matches);if(value != 0)return value;
        }
        return a.Player.Id.CompareTo(b.Player.Id);
    }
    private static PlayerRankingRowDto Row(GameWorld w, Total t, int rank, string category)
    {
        var p=t.Player;
        decimal? average=t.Incomplete || category=="assists"&&t.IncompleteAssists ? null : (category=="assists"?t.Assists:category=="scorers"?t.Goals:category=="goalkeepers"?t.Conceded:t.Yellow+3L*t.Red)/(decimal)Math.Max(1,t.Matches);
        return new(rank,p.Id,p.DisplayName,p.Position,p.Strength,p.Behavior.ToDisplayPt(),p.IsStar,p.ClubId,
            w.Clubs.FirstOrDefault(c=>c.Id==p.ClubId)?.Name??"Sem clube",t.Matches,t.Goals,t.Conceded,t.Yellow,t.Red,t.Minutes,average,
            t.Red/(decimal)Math.Max(1,t.Matches),t.Yellow/(decimal)Math.Max(1,t.Matches),category is "scorers" or "assists"?0:Math.Max(0,20-t.Matches),p.SeasonTopScorerTitles)
            { Assists = t.Assists, HasIncompleteAssists = t.IncompleteAssists };
    }
    public static PlayerRankingsDto GetRankings(GameWorld w, string category="scorers", string scope="general", string period="career", int? season=null)
    {
        if (!new[]{"scorers","assists","goalkeepers","defenders"}.Contains(category) || !new[]{"general","division1","division2","division3","division4","cup"}.Contains(scope)
            || !new[]{"career","current","season"}.Contains(period) || (period=="season" && (season is null or <1 || season>w.SeasonNumber))) throw new ArgumentException("Filtro de ranking inválido.");
        EnsureMigrated(w);
        int? selected = period=="career"?null:period=="current"?w.SeasonNumber:season;
        var totals=Totals(w,scope,selected,category);totals.Sort((a,b)=>Compare(a,b,category));
        bool Eligible(Total t)=>category=="assists"?t.Assists>0:category=="scorers"?t.Goals>0:t.Matches>=20&&!t.Incomplete;
        var warnings = w.PlayerStatisticsCoverageWarnings.ToList();
        if (category == "assists" && totals.Any(t => t.IncompleteAssists))
            warnings.Add("Assistências anteriores à atualização não possuem registro completo do passe; não foram reconstruídas. O ranking soma somente assistências registradas.");
        return new(category,scope,period,selected,w.SeasonNumber,category is "scorers" or "assists"?0:20,
            totals.Where(Eligible).Select((t,i)=>Row(w,t,i+1,category)).ToList(),
            category is "scorers" or "assists"?new():totals.Where(t=>!Eligible(t)).Select(t=>Row(w,t,0,category)).ToList(),
            w.PlayerStatistics.Select(s=>s.Season).Concat(w.SeasonHistories.Select(h=>h.SeasonNumber)).Append(w.SeasonNumber).Distinct().OrderByDescending(x=>x).ToList(),
            warnings);
    }
    public static string ResolveHistoricalClubName(GameWorld w, Guid clubId, string? recordedName = null)
    {
        if (!string.IsNullOrWhiteSpace(recordedName)) return recordedName;
        var club = w.Clubs.FirstOrDefault(c => c.Id == clubId);
        if (!string.IsNullOrWhiteSpace(club?.Name)) return club!.Name;
        var awardName = w.SeasonTopScorerAwards.FirstOrDefault(a => a.ClubId == clubId && !string.IsNullOrWhiteSpace(a.ClubNameAtClose))?.ClubNameAtClose;
        if (!string.IsNullOrWhiteSpace(awardName)) return awardName!;
        var employmentName = w.HumanCoaches.SelectMany(h => h.EmploymentHistory)
            .Concat(w.AiManagers.SelectMany(m => m.EmploymentHistory))
            .FirstOrDefault(e => e.ClubId == clubId && !string.IsNullOrWhiteSpace(e.ClubName))?.ClubName;
        if (!string.IsNullOrWhiteSpace(employmentName)) return employmentName!;
        return "Clube histórico indisponível";
    }
    public static PlayerHistoryDto? GetHistory(GameWorld w, Guid playerId)
    {
        EnsureMigrated(w);var p=w.Players.FirstOrDefault(p=>p.Id==playerId);if(p==null)return null;
        var total=Totals(w,"general",null).FirstOrDefault(t=>t.Player.Id==playerId)??new(p,0,0,0,0,0,0,false);
        var row=Row(w,total,0,"scorers");
        return HistoryEngine.EnrichPlayer(w,new(row,row,w.PlayerStatistics.Where(s=>s.PlayerId==playerId).OrderByDescending(s=>s.Season).ThenBy(s=>s.CompetitionType).ThenBy(s=>s.ClubId)
            .Select(s=>new PlayerStatisticsRowDto(s.Season,s.CompetitionType,s.Division,s.ClubId,s.MatchesPlayed,s.GoalsScored,s.GoalsConceded,s.YellowCards,s.RedCards,s.MinutesPlayed,s.HasIncompleteAppearances,s.HasIncompleteGoals,ResolveHistoricalClubName(w,s.ClubId,s.ClubName)){Assists=s.Assists,HasIncompleteAssists=s.HasIncompleteAssists}).ToList(),
            w.SeasonTopScorerAwards.Where(a=>a.PlayerId==playerId).OrderByDescending(a=>a.Season).Select(AwardDto).ToList(),w.PlayerStatisticsCoverageWarnings.Concat(w.DisciplineCoverageWarnings).Distinct().ToList())
        {
            StrengthHistory = w.PlayerStrengthHistory.Where(e => e.PlayerId == playerId)
                .OrderByDescending(e => e.Season).ThenByDescending(e => e.Round).ToList(),
            StrengthHistoryStartSeason = w.PlayerStrengthHistoryStartSeason,
            StrengthHistoryStartRound = w.PlayerStrengthHistoryStartRound,
            CardHistory = p.CardHistory.ToList(),
            SuspensionHistory = p.SuspensionHistory.ToList()
        });
    }
    private static PlayerAwardDto AwardDto(SeasonTopScorerAward a)=>new(a.AwardId,a.PlayerId,a.DisplayName,a.Season,a.TotalGoals,a.TotalMatches,a.ClubId,a.ClubNameAtClose,a.AwardAmount,a.AwardGrantedAt,a.IdempotencyKey);
    public static SeasonTopScorerAwardsDto GetSeasonAwards(GameWorld w,int? season=null)
    {
        EnsureMigrated(w);
        int? selected=season??(w.ClosedPlayerAwardSeasons.Count>0?w.ClosedPlayerAwardSeasons.Max():null);
        var awards=w.SeasonTopScorerAwards.Where(a=>a.Season==selected).OrderBy(a=>a.PlayerId).Select(AwardDto).ToList();
        return new(w.SeasonNumber,selected,awards,awards.Where(a=>a.ClubId.HasValue).GroupBy(a=>a.ClubId!.Value)
            .Select(g=>new AwardClubTotalDto(g.Key,g.First().ClubNameAtClose,g.Sum(a=>a.AwardAmount))).ToList(),w.PlayerStatisticsCoverageWarnings.ToList());
    }
    public static List<SeasonTopScorerAward> AwardSeason(GameWorld w,int season)
    {
        EnsureMigrated(w);
        if(w.ClosedPlayerAwardSeasons.Contains(season))return w.SeasonTopScorerAwards.Where(a=>a.Season==season).ToList();
        var matches=w.Fixtures.Where(f=>f.SeasonNumber==season).ToList();
        if(season!=w.SeasonNumber || w.RoundNumber<w.TotalRoundsInSeason || matches.Count==0 || matches.Any(f=>!f.IsFinished))return new();
        ApplyFinishedMatches(w,matches);
        if(w.PlayerStatistics.Any(s=>s.Season==season&&(s.HasIncompleteAppearances||s.HasIncompleteGoals))) {
            Warn(w,$"Temporada {season}: premiação não concedida automaticamente porque os eventos/escalações disponíveis são incompletos.");return new();
        }
        var totals=Totals(w,"general",season);int max=totals.Select(t=>t.Goals).DefaultIfEmpty(0).Max();
        foreach(var t in totals.Where(t=>t.Goals==max&&max>0).OrderBy(t=>t.Player.Id))
        {
            var p=t.Player;string key=$"season_top_scorer:{w.Id}:{season}:{p.Id}";
            if(w.SeasonTopScorerAwards.Any(a=>a.IdempotencyKey==key))continue;
            var club=w.Clubs.FirstOrDefault(c=>c.Id==p.ClubId);decimal amount=club==null?0m:TopScorerPrize;
            if(club!=null)MarketAndEconomyEngine.TryRecordTransaction(w,club,FinancialTransactionCategory.PrizeMoney,amount,
                $"ARTILHEIRO DA TEMPORADA {season}: {p.DisplayName}, {max} gols",key);
            var award=new SeasonTopScorerAward(CoachCareerEngine.StableId(key),p.Id,p.DisplayName,season,max,t.Matches,club?.Id,club?.Name??"Sem clube — conquista sem pagamento",amount,DateTime.UtcNow,key);
            w.SeasonTopScorerAwards.Add(award);p.SeasonTopScorerTitles++;
            CoachCareerEngine.Log(w,"SEASON_TOP_SCORER",null,club?.Id,$"{p.DisplayName}: {max} gols; prêmio {amount}; {key}");
        }
        w.ClosedPlayerAwardSeasons.Add(season);
        return w.SeasonTopScorerAwards.Where(a=>a.Season==season).ToList();
    }
}
