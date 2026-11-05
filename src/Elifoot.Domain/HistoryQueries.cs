using Elifoot.Contracts;
namespace Elifoot.Domain;

public static partial class HistoryEngine
{
    public static List<string> Warnings(GameWorld w)=>w.History.StartSeason>1||w.History.StartRound>1||w.History.ClubSeasons.Values.Any(s=>s.Partial)
        ?["Histórico parcial: somente registros efetivamente preservados no save. Campos antigos desconhecidos não foram estimados."]:[];
    public static HistoryTotalsDto Totals(IEnumerable<HistoryMatchDto> matches,Guid club)
    {
        int games=0,wins=0,draws=0,losses=0,scored=0,conceded=0;
        foreach(var m in matches)
        {
            int gf=m.HomeClubId==club?m.HomeGoals:m.AwayGoals,ga=m.HomeClubId==club?m.AwayGoals:m.HomeGoals;
            int result=m.Competition!=CompetitionType.League&&m.WinnerClubId.HasValue?(m.WinnerClubId==club?1:-1):Math.Sign(gf-ga);
            games++;scored+=gf;conceded+=ga;if(result>0)wins++;else if(result==0)draws++;else losses++;
        }
        return new(games,wins,draws,losses,scored,conceded);
    }
    public static ClubHistoryDto? GetClubHistory(GameWorld w,Guid id,int? season=null)
    {
        var c=w.Clubs.FirstOrDefault(c=>c.Id==id);if(c==null)return null;
        var all=w.History.Matches.Values.Select(m=>m.Match).Where(m=>m.HomeClubId==id||m.AwayClubId==id).OrderBy(m=>m.Season).ThenBy(m=>m.Round).ThenBy(m=>m.MatchId).ToList();
        var selected=all.Where(m=>season==null||m.Season==season).ToList();
        var seasons=w.History.ClubSeasons.Values.Where(s=>s.ClubId==id&&(season==null||s.Season==season)).OrderByDescending(s=>s.Season).Select(s=>
        {
            var games=all.Where(m=>m.Season==s.Season).ToList();
            var cup=games.Where(m=>m.Competition!=CompetitionType.League).ToList();
            var last=cup.OrderByDescending(m=>m.Round).FirstOrDefault();
            string? campaign=last==null?null:!last.WinnerClubId.HasValue?$"{last.Competition}: resultado decisivo não registrado":last.Competition==CompetitionType.CupFinal&&last.WinnerClubId==id?"Campeão":last.Competition==CompetitionType.CupFinal?"Vice-campeão":$"{last.Competition}: {(last.WinnerClubId==id?"Classificado":"Eliminado")}";
            var ledger=w.Transactions.Where(t=>t.ClubId==id&&t.SeasonNumber==s.Season).ToList();
            return new ClubSeasonHistoryDto(s.Season,s.Division,s.FinalPosition,s.Closed,s.Partial,s.Promoted,s.Relegated,campaign,
                Totals(games,id),Totals(games.Where(m=>m.Competition==CompetitionType.League),id),Totals(cup,id),s.OpeningCash,s.ClosingCash,
                s.Closed?s.Income:ledger.Where(t=>t.Amount>0).Sum(t=>t.Amount),s.Closed?s.Expenses:-ledger.Where(t=>t.Amount<0).Sum(t=>t.Amount),
                w.PlayerStatistics.Where(p=>p.ClubId==id&&p.Season==s.Season).GroupBy(p=>p.PlayerId).Select(g=>new HistoryScorerDto(g.Key,PlayerName(w,g.Key),g.Sum(x=>x.GoalsScored))).Where(x=>x.Goals>0).OrderByDescending(x=>x.Goals).ThenBy(x=>x.PlayerId).ToList());
        }).ToList();
        int diff(HistoryMatchDto m)=>m.HomeClubId==id?m.HomeGoals-m.AwayGoals:m.AwayGoals-m.HomeGoals;
        int run=0,unbeaten=0,best=0,bestUnbeaten=0;
        foreach(var m in selected) { int result=m.Competition!=CompetitionType.League&&m.WinnerClubId.HasValue?(m.WinnerClubId==id?1:-1):diff(m);run=result>0?run+1:0;unbeaten=result>=0?unbeaten+1:0;best=Math.Max(best,run);bestUnbeaten=Math.Max(bestUnbeaten,unbeaten); }
        var records=new HistoryRecordsDto(selected.Where(m=>diff(m)>0).OrderByDescending(diff).ThenByDescending(m=>m.HomeClubId==id?m.HomeGoals:m.AwayGoals).FirstOrDefault(),
            selected.Where(m=>diff(m)<0).OrderBy(diff).ThenByDescending(m=>m.HomeClubId==id?m.AwayGoals:m.HomeGoals).FirstOrDefault(),seasons.Any(s=>s.Partial)?null:best,seasons.Any(s=>s.Partial)?null:bestUnbeaten);
        var coaches=new List<HistoryCoachDto>();
        foreach(var coach in CoachCareerEngine.Coaches(w))
        foreach(var e in coach.Employment.Where(e=>e.ClubId==id))
        {
            long from=(long)e.StartSeason*10000+e.StartRound,to=e.EndSeason.HasValue?(long)e.EndSeason.Value*10000+e.EndRound.GetValueOrDefault():long.MaxValue;
            var games=w.History.Matches.Values.Where(a=>(a.Match.HomeClubId==id?a.HomeCoachId:a.AwayCoachId)==coach.Id&&(a.Match.HomeClubId==id||a.Match.AwayClubId==id)
                &&(long)a.Match.Season*10000+a.Match.Round>=from&&(long)a.Match.Season*10000+a.Match.Round<=to&&(season==null||a.Match.Season==season)).Select(a=>a.Match).ToList();
            int titles=w.CoachTitles.Count(t=>t.CoachId==coach.Id&&t.ClubId==id&&(season==null||t.Season==season)&&t.Season>=e.StartSeason&&t.Season<=(e.EndSeason??int.MaxValue)
                &&games.Any(m=>m.MatchId==all.Where(m=>m.Season==t.Season&&(t.TitleType=="CUP"?m.Competition==CompetitionType.CupFinal:m.Competition==CompetitionType.League&&m.Division==t.Division)).OrderByDescending(m=>m.Round).FirstOrDefault()?.MatchId));
            coaches.Add(new(coach.Id,coach.Name,coach.Human!=null,e.StartSeason,e.StartRound,e.EndSeason,e.EndRound,e.ReasonForLeaving,Totals(games,id),titles,
                e.StartSeason<w.History.StartSeason||e.StartSeason==w.History.StartSeason&&e.StartRound<w.History.StartRound));
        }
        var players=w.History.Memberships.Where(p=>p.ClubId==id&&(season==null||p.StartSeason<=season&&(p.EndSeason==null||p.EndSeason>=season))).ToList();
        foreach(var g in w.PlayerStatistics.Where(s=>s.ClubId==id&&(season==null||s.Season==season)).GroupBy(s=>s.PlayerId))
            if(!players.Any(m=>m.PlayerId==g.Key&&m.StartSeason<=g.Min(s=>s.Season)&&(m.EndSeason==null||m.EndSeason>=g.Max(s=>s.Season))))
                players.Add(new(g.Key,PlayerName(w,g.Key),id,g.First().ClubName,g.Min(s=>s.Season),0,g.Max(s=>s.Season),0,true));
        return new(id,c.Name,c.DivisionTier,seasons,Totals(selected,id),w.History.Titles.Where(t=>t.ClubId==id&&(season==null||t.Season==season)).OrderByDescending(t=>t.Season).ToList(),coaches,players,
            w.History.Transfers.Values.Where(t=>(t.FromClubId==id||t.ToClubId==id)&&(season==null||t.Season==season)).OrderByDescending(t=>t.Season).ThenByDescending(t=>t.Round).ToList(),records,Warnings(w));
    }
    public static List<HistoryMembershipDto> Memberships(GameWorld w,Guid player)
    {
        var result=w.History.Memberships.Where(m=>m.PlayerId==player).ToList();
        foreach(var g in w.PlayerStatistics.Where(s=>s.PlayerId==player).GroupBy(s=>s.ClubId))
        {
            int start=g.Min(x=>x.Season),end=g.Max(x=>x.Season);
            if(!result.Any(m=>m.ClubId==g.Key&&m.StartSeason<=start&&(m.EndSeason==null||m.EndSeason>=end)))
                result.Add(new(player,PlayerName(w,player),g.Key,g.First().ClubName,start,0,end,0,true));
        }
        return result.OrderBy(x=>x.StartSeason).ThenBy(x=>x.StartRound).ToList();
    }
    public static PlayerHistoryDto EnrichPlayer(GameWorld w,PlayerHistoryDto dto)
    {
        Guid id=dto.Player.PlayerId;
        var stats=w.PlayerStatistics.Where(p=>p.PlayerId==id).ToList();
        var strengths=w.History.Strengths.Values.Where(p=>p.PlayerId==id).OrderBy(p=>p.Season).ToList();
        var peak=w.PlayerStrengthHistory.Where(e=>e.PlayerId==id).SelectMany(e=>new[]{e.StrengthBefore,e.StrengthAfter})
            .Concat(strengths.SelectMany(s=>new[]{s.Initial??0,s.Final??0})).Append(dto.Player.Strength).Max();
        return dto with {
            ClubCareer=Memberships(w,id),Transfers=w.History.Transfers.Values.Where(t=>t.PlayerId==id).OrderByDescending(t=>t.Season).ThenByDescending(t=>t.Round).ToList(),
            ClubTitlesWithParticipation=w.History.Titles.Where(t=>!t.RunnerUp&&stats.Any(s=>s.Season==t.Season&&s.ClubId==t.ClubId&&s.MatchesPlayed>0&&(t.Competition=="LEAGUE"?s.CompetitionType==CompetitionType.League&&s.Division==t.Division:s.CompetitionType!=CompetitionType.League))).ToList(),
            ScoringRecognitions=w.History.Scorers.Where(s=>s.PlayerId==id).OrderByDescending(s=>s.Season).ToList(),
            SeasonStrengths=strengths.Select(s=>new PlayerSeasonStrengthDto(s.Season,s.Initial,s.Final,s.Partial)).ToList(),HighestRecordedStrength=peak,
            MostGoalsInSeason=stats.GroupBy(s=>s.Season).Select(g=>g.Sum(s=>s.GoalsScored)).DefaultIfEmpty().Max(),
            MostGamesInSeason=stats.GroupBy(s=>s.Season).Select(g=>g.Sum(s=>s.MatchesPlayed)).DefaultIfEmpty().Max(),
            SeasonTotals=stats.GroupBy(s=>s.Season).OrderByDescending(g=>g.Key).Select(g=>new HistoryPlayerSeasonTotalsDto(g.Key,g.Sum(s=>s.MatchesPlayed),g.Sum(s=>s.GoalsScored),g.Sum(s=>s.Assists),g.Sum(s=>s.MinutesPlayed),g.Any(s=>s.HasIncompleteAppearances||s.HasIncompleteGoals),g.Any(s=>s.HasIncompleteAssists))).ToList(),
            CoverageWarnings=dto.CoverageWarnings.Concat(Warnings(w)).Distinct().ToList()
        };
    }
}
