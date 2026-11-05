using Elifoot.Contracts;
namespace Elifoot.Domain;

/// Compact, idempotent telemetry over persisted sporting and financial events. No RNG or decisions.
public static partial class HistoryEngine
{
    private static string Key(Guid id,int season)=>$"{id}:S{season}";
    public static void Observe(GameWorld w,bool beginningSeason=false)
    {
        var h=w.History;
        bool migrating=h.Version==0;
        bool pristine=beginningSeason||w.SeasonNumber==1&&w.RoundNumber==1&&w.Fixtures.All(f=>f.CurrentMinute==0);
        if(h.Version==0) { h.Version=1;h.StartSeason=w.SeasonNumber;h.StartRound=w.RoundNumber; }
        foreach(var c in w.Clubs)
            if(!h.ClubSeasons.ContainsKey(Key(c.Id,w.SeasonNumber)))
                h.ClubSeasons[Key(c.Id,w.SeasonNumber)]=new(){ClubId=c.Id,Season=w.SeasonNumber,Division=c.DivisionTier,
                    Partial=!pristine,OpeningCash=pristine?c.CashBalance:null};
        foreach(var p in w.Players)
            if(!h.Strengths.ContainsKey(Key(p.Id,w.SeasonNumber)))
                h.Strengths[Key(p.Id,w.SeasonNumber)]=new(){PlayerId=p.Id,Season=w.SeasonNumber,Initial=pristine?p.Strength:null,Partial=!pristine};

        foreach(var m in w.Fixtures.Where(m=>ClassicEconomyEngine.Played(m)))
        {
            h.Matches.TryAdd(m.Id,new(new(m.Id,m.SeasonNumber,m.RoundNumber,m.CompetitionType,m.DivisionTier,
                m.HomeClubId,ClubName(w,m.HomeClubId),m.HomeGoals,m.AwayClubId,ClubName(w,m.AwayClubId),m.AwayGoals,m.WinnerClubId),
                m.CareerLeadershipCaptured?m.HomeCoachAtKickoffId:null,m.CareerLeadershipCaptured?m.AwayCoachAtKickoffId:null));
            foreach(var id in new[]{m.HomeClubId,m.AwayClubId})
                if(!h.ClubSeasons.ContainsKey(Key(id,m.SeasonNumber)))h.ClubSeasons[Key(id,m.SeasonNumber)]=new(){ClubId=id,Season=m.SeasonNumber,Division=m.CompetitionType==CompetitionType.League?m.DivisionTier:null,Closed=m.SeasonNumber<w.SeasonNumber};
        }
        foreach(var stat in migrating?w.PlayerStatistics:[])
            if(!h.ClubSeasons.ContainsKey(Key(stat.ClubId,stat.Season)))h.ClubSeasons[Key(stat.ClubId,stat.Season)]=new(){ClubId=stat.ClubId,Season=stat.Season,Division=stat.Division,Closed=stat.Season<w.SeasonNumber};

        if(migrating)foreach(var entry in w.SeasonHistories)
        {
            foreach(var id in entry.PromotedClubIds.Concat(entry.RelegatedClubIds).Append(entry.FirstDivisionChampionClubId).Where(id=>w.Clubs.Any(c=>c.Id==id)).Distinct())
            {
                if(!h.ClubSeasons.TryGetValue(Key(id,entry.SeasonNumber),out var record))h.ClubSeasons[Key(id,entry.SeasonNumber)]=record=new(){ClubId=id,Season=entry.SeasonNumber,Closed=true};
                record.Promoted=entry.PromotedClubIds.Contains(id);record.Relegated=entry.RelegatedClubIds.Contains(id);
                if(id==entry.FirstDivisionChampionClubId){record.Division=DivisionTier.PrimeiraDivisao;record.FinalPosition=1;AddTitle(w,new(entry.SeasonNumber,"LEAGUE",DivisionTier.PrimeiraDivisao,id,ClubName(w,id)));}
            }
        }

        // The pair of transfer ledger entries shares one stable execution identity.
        var sold=w.Economy.AuctionHistory.Where(a=>a.Outcome=="SOLD"&&a.BuyerClubId.HasValue).DistinctBy(a=>a.AuctionId).ToDictionary(a=>a.AuctionId);
        foreach(var t in w.Transactions.Where(t=>t.PlayerId.HasValue&&t.BuyerClubId.HasValue))
        {
            var id=t.TransferTransactionId??t.Id;
            foreach(var part in t.IdempotencyKey.Split(':'))if(Guid.TryParse(part,out var auctionId)&&sold.ContainsKey(auctionId)){id=auctionId;break;}
            h.Transfers.TryAdd(id,new(id,t.PlayerId!.Value,PlayerName(w,t.PlayerId.Value),t.SellerClubId,ClubName(w,t.SellerClubId),
                t.BuyerClubId,ClubName(w,t.BuyerClubId),t.SeasonNumber,t.RoundNumber,Math.Abs(t.Amount),t.TransactionType??t.Category.ToString()));
        }
        foreach(var a in sold.Values)h.Transfers.TryAdd(a.AuctionId,new(a.AuctionId,a.PlayerId,PlayerName(w,a.PlayerId),a.FormerClubId,ClubName(w,a.FormerClubId),
            a.BuyerClubId,ClubName(w,a.BuyerClubId),a.Season,a.Round,a.Price,a.Reason.ToString()));
        var active=h.Memberships.Select((x,i)=>(x,i)).Where(e=>e.x.EndSeason==null).ToDictionary(e=>e.x.PlayerId);
        foreach(var p in w.Players)
        {
            if(active.TryGetValue(p.Id,out var old)&&old.x.ClubId==p.ClubId)continue;
            if(active.TryGetValue(p.Id,out old))h.Memberships[old.i]=old.x with{EndSeason=w.SeasonNumber,EndRound=w.RoundNumber};
            h.Memberships.Add(new(p.Id,p.DisplayName,p.ClubId,ClubName(w,p.ClubId),w.SeasonNumber,w.RoundNumber,null,null,!pristine&&old.x==null));
        }
        // Reliable pre-existing titles are reused; legacy summary fallback champions are not inferred.
        var titleKeys=h.Titles.Select(t=>(t.Season,t.Competition,t.Division,t.ClubId,t.RunnerUp)).ToHashSet();
        foreach(var t in w.CoachTitles)
            if(titleKeys.Add((t.Season,t.TitleType,t.Division,t.ClubId,false)))h.Titles.Add(new(t.Season,t.TitleType,t.Division,t.ClubId,ClubName(w,t.ClubId)));
        foreach(var a in w.SeasonTopScorerAwards)
            if(!h.Scorers.Any(s=>s.PlayerId==a.PlayerId&&s.Season==a.Season&&s.Scope=="general"))h.Scorers.Add(new(a.PlayerId,a.Season,"general",a.TotalGoals));
    }

    public static void CloseSeason(GameWorld w,SeasonHistoryEntry entry,IEnumerable<List<DivisionStandingRowDto>> tables)
    {
        Observe(w);
        if(!w.History.ClosedSeasons.Add(entry.SeasonNumber))return;
        foreach(var table in tables)
        {
            if(table.Count==0)continue;
            var tier=w.History.ClubSeasons.GetValueOrDefault(Key(table[0].ClubId,entry.SeasonNumber))?.Division;
            var games=w.Fixtures.Where(m=>m.SeasonNumber==entry.SeasonNumber&&m.CompetitionType==CompetitionType.League&&m.DivisionTier==tier&&!m.IsCancelled&&!m.IsPostponed).ToList();
            if(games.Count==0||games.Any(m=>!ClassicEconomyEngine.Played(m)))continue;
            foreach(var row in table)
            {
                if(!w.History.ClubSeasons.TryGetValue(Key(row.ClubId,entry.SeasonNumber),out var record))continue;
                record.FinalPosition=row.Position;
                if(row.Position<=2)AddTitle(w,new(entry.SeasonNumber,"LEAGUE",tier,row.ClubId,ClubName(w,row.ClubId),row.Position==2));
            }
            RecordScorers(w,entry.SeasonNumber,"division"+(int)tier!.Value);
        }
        var final=w.Fixtures.FirstOrDefault(m=>m.SeasonNumber==entry.SeasonNumber&&m.CompetitionType==CompetitionType.CupFinal&&ClassicEconomyEngine.Played(m));
        if(final?.WinnerClubId is Guid winner&&(winner==final.HomeClubId||winner==final.AwayClubId))
        {
            AddTitle(w,new(entry.SeasonNumber,"CUP",null,winner,ClubName(w,winner)));
            var runner=winner==final.HomeClubId?final.AwayClubId:final.HomeClubId;
            AddTitle(w,new(entry.SeasonNumber,"CUP",null,runner,ClubName(w,runner),true));
            RecordScorers(w,entry.SeasonNumber,"cup");
        }
        foreach(var award in w.SeasonTopScorerAwards.Where(a=>a.Season==entry.SeasonNumber))
            if(!w.History.Scorers.Any(a=>a.PlayerId==award.PlayerId&&a.Season==award.Season&&a.Scope=="general"))
                w.History.Scorers.Add(new(award.PlayerId,award.Season,"general",award.TotalGoals));
        foreach(var record in w.History.ClubSeasons.Values.Where(r=>r.Season==entry.SeasonNumber))
        {
            record.Closed=true;record.Promoted=entry.PromotedClubIds.Contains(record.ClubId);record.Relegated=entry.RelegatedClubIds.Contains(record.ClubId);
            record.ClosingCash=w.Clubs.FirstOrDefault(c=>c.Id==record.ClubId)?.CashBalance;
            var ledger=w.Transactions.Where(t=>t.ClubId==record.ClubId&&t.SeasonNumber==entry.SeasonNumber);
            record.Income=ledger.Where(t=>t.Amount>0).Sum(t=>t.Amount);record.Expenses=-ledger.Where(t=>t.Amount<0).Sum(t=>t.Amount);
        }
        foreach(var p in w.Players)if(w.History.Strengths.TryGetValue(Key(p.Id,entry.SeasonNumber),out var strength))strength.Final=p.Strength;
    }
    private static void RecordScorers(GameWorld w,int season,string scope)
    {
        var rows=PlayerStatisticsEngine.GetRankings(w,"scorers",scope,"season",season).Rows;
        int max=rows.Select(r=>r.GoalsScored).DefaultIfEmpty().Max();
        foreach(var r in rows.Where(r=>r.GoalsScored>0&&r.GoalsScored==max))
            if(!w.History.Scorers.Any(x=>x.PlayerId==r.PlayerId&&x.Season==season&&x.Scope==scope))w.History.Scorers.Add(new(r.PlayerId,season,scope,r.GoalsScored));
    }
    private static void AddTitle(GameWorld w,HistoryTitleDto title)
    { if(!w.History.Titles.Any(t=>t.Season==title.Season&&t.Competition==title.Competition&&t.Division==title.Division&&t.ClubId==title.ClubId&&t.RunnerUp==title.RunnerUp))w.History.Titles.Add(title); }
    public static string ClubName(GameWorld w,Guid? id)=>id==null?"Sem clube":w.Clubs.FirstOrDefault(c=>c.Id==id)?.Name??"Clube não disponível";
    public static string PlayerName(GameWorld w,Guid id)=>w.Players.FirstOrDefault(p=>p.Id==id)?.DisplayName??"Jogador não disponível";
}
