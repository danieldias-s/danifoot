using System.Buffers.Binary;
using Elifoot.Contracts;

namespace Elifoot.Domain;

public class MatchDisciplineState
{
    public bool KickoffCaptured { get; set; }
    public List<SuspendedAtKickoff> SuspendedPlayers { get; set; } = new();
    public Dictionary<Guid, MatchPlayerDiscipline> Cards { get; set; } = new();
}
public record SuspendedAtKickoff(Guid PlayerId, Guid ClubId, int Balance);
public class MatchPlayerDiscipline
{
    public int CycleBefore { get; set; }
    public int BalanceBefore { get; set; }
    public string? ReasonBefore { get; set; }
    public int Yellows { get; set; }
    public int DirectBan { get; set; }
    public bool SecondYellowDismissal { get; set; }
}

/// <summary>Global sporting suspensions, independent of finance, force rules and wall-clock days.</summary>
public static class DisciplineEngine
{
    public const int Version = 1;
    public static int DirectRedDuration(int roll) => roll switch
    {
        >= 0 and < 50 => 1, < 80 and >= 50 => 2, < 95 and >= 80 => 3,
        < 100 and >= 95 => 4, _ => throw new ArgumentOutOfRangeException(nameof(roll))
    };
    public static int DrawDirectRedDuration(MatchFixture m, Player p, int seed, int? minute=null)
    {
        int token=BinaryPrimitives.ReadInt32LittleEndian(p.Id.ToByteArray()) & 0x3fffffff;
        var rng=DeterministicRng.FromContext(seed,m.SeasonNumber,m.RoundNumber,m.DeterministicMatchIndex,minute??m.CurrentMinute,21021+token);
        return DirectRedDuration(rng.NextInt(0,100));
    }

    public static void EnsureMigrated(GameWorld w)
    {
        if(w.DisciplineRulesVersion==Version)return;
        if(w.DisciplineRulesVersion!=0)throw new InvalidOperationException("Unsupported discipline rules version.");
        var stats=w.PlayerStatistics.GroupBy(s=>s.PlayerId).ToDictionary(g=>g.Key,g=>g.ToList());
        var oldCards=w.Fixtures.SelectMany(m=>m.Events.Where(IsCard).Select(e=>(Match:m,Event:e)))
            .Where(x=>x.Event.PrimaryPlayerId!=null).GroupBy(x=>x.Event.PrimaryPlayerId!.Value).ToDictionary(g=>g.Key,g=>g.ToList());
        foreach(var p in w.Players)
        {
            if(p.DisciplineVersion==Version)continue;
            var known=oldCards.GetValueOrDefault(p.Id)??[];
            int yellows=known.Count(x=>x.Event.EventType is MatchEventType.YellowCard or MatchEventType.SecondYellowRedCard);
            p.TotalYellowCards=Math.Max(p.YellowCardsInCurrentCycle,Math.Max(p.TotalYellowCards,Math.Max(p.SeasonYellows,Math.Max(yellows,stats.GetValueOrDefault(p.Id)?.Sum(s=>s.YellowCards)??0))));
            p.TotalDirectRedCards=Math.Max(p.TotalDirectRedCards,known.Count(x=>x.Event.EventType==MatchEventType.DirectRedCard));
            p.TotalSecondYellowDismissals=Math.Max(p.TotalSecondYellowDismissals,known.Count(x=>x.Event.EventType==MatchEventType.SecondYellowRedCard));
            int allReds=Math.Max(p.SeasonReds,stats.GetValueOrDefault(p.Id)?.Sum(s=>s.RedCards)??0);
            p.UnclassifiedHistoricalRedCards=Math.Max(p.UnclassifiedHistoricalRedCards,allReds-p.TotalDirectRedCards-p.TotalSecondYellowDismissals);
            foreach(var x in known)
            {
                string key=CardKey(x.Match,p,x.Event);
                if(p.CardHistory.All(e=>e.Key!=key))p.CardHistory.Add(Card(x.Match,p,x.Event,0,true));
            }
            p.DisciplineVersion=Version;
            if(p.SuspensionMatchesRemaining>0 && string.IsNullOrEmpty(p.SuspensionReason))p.SuspensionReason="LEGACY_UNSPECIFIED";
        }
        foreach(var m in w.Fixtures.Where(m=>m.FinancialsSettled))
            w.SettledDisciplineMatches.Add(m.Id); // Never charge another served match for already settled legacy fixtures.
        var byId=w.Players.ToDictionary(p=>p.Id);
        foreach(var m in w.Fixtures.Where(m=>!m.IsFinished&&m.CurrentMinute>0))
        {
            // The old explicit third-yellow marker proves that this live cycle started at two.
            foreach(var group in m.Events.Where(IsCard).Where(e=>e.PrimaryPlayerId!=null).GroupBy(e=>e.PrimaryPlayerId!.Value))
            {
                if(!byId.TryGetValue(group.Key,out var p))continue;
                var events=group.ToList();bool second=events.Any(e=>e.EventType==MatchEventType.SecondYellowRedCard);
                int yellows=events.Count(e=>e.EventType is MatchEventType.YellowCard or MatchEventType.SecondYellowRedCard);
                bool third=m.HomeSuspendedByYellowInMatchIds.Contains(p.Id)||m.AwaySuspendedByYellowInMatchIds.Contains(p.Id);
                int direct=events.LastOrDefault(e=>e.EventType==MatchEventType.DirectRedCard)?.DisciplinarySuspensionMatches
                    ??(events.Any(e=>e.EventType==MatchEventType.DirectRedCard)?2:0);
                int award=Math.Max(direct,second||third?1:0);
                var state=new MatchPlayerDiscipline {CycleBefore=third?2:second?p.YellowCardsInCurrentCycle:Math.Max(0,p.YellowCardsInCurrentCycle-(yellows>0?1:0)),
                    BalanceBefore=p.SuspensionMatchesRemaining>award?p.SuspensionMatchesRemaining:0,ReasonBefore=p.SuspensionReason,
                    Yellows=yellows,SecondYellowDismissal=second,DirectBan=direct};
                m.Discipline.Cards[p.Id]=state;
                if(second&&third)p.YellowCardsInCurrentCycle=2;
                p.TotalYellowCards=Math.Max(p.TotalYellowCards,state.CycleBefore+yellows);
            }
            m.LastSimulatedMinute=Math.Max(m.LastSimulatedMinute,m.CurrentMinute);
            w.DisciplineCoverageWarnings.Add($"Legacy live match {m.Id}: cycle reconstructed from explicit card markers; earlier suspension origin may be unavailable.");
        }
        if(w.SeasonNumber>1||w.Players.Any(p=>p.TotalYellowCards>p.CardHistory.Count(e=>e.Card is MatchEventType.YellowCard or MatchEventType.SecondYellowRedCard)||p.UnclassifiedHistoricalRedCards>0))
            w.DisciplineCoverageWarnings.Add("Legacy: known totals and retained card events preserved; missing historical dates and red-card types were not invented.");
        w.DisciplineRulesVersion=Version;
    }

    public static void CaptureKickoff(MatchFixture m,IReadOnlyDictionary<Guid,Player> players)
    {
        if(m.Discipline.KickoffCaptured)return;
        foreach(var p in players.Values.Where(p=>p.ClubId==m.HomeClubId||p.ClubId==m.AwayClubId))
            if(p.SuspensionMatchesRemaining>0 && !m.HomeSentOffIds.Contains(p.Id)&&!m.AwaySentOffIds.Contains(p.Id)
                &&!m.HomeSuspendedByYellowInMatchIds.Contains(p.Id)&&!m.AwaySuspendedByYellowInMatchIds.Contains(p.Id))
                m.Discipline.SuspendedPlayers.Add(new(p.Id,p.ClubId!.Value,p.SuspensionMatchesRemaining));
        m.Discipline.KickoffCaptured=true;
    }

    public static bool IsCard(MatchEventRecord e)=>e.EventType is MatchEventType.YellowCard or MatchEventType.SecondYellowRedCard or MatchEventType.DirectRedCard;
    private static string CardKey(MatchFixture m,Player p,MatchEventRecord e)=>$"{m.GameWorldId}:M{m.Id}:P{p.Id}:{e.Minute}:{e.EventType}";
    private static PlayerCardHistoryDto Card(MatchFixture m,Player p,MatchEventRecord e,int duration,bool legacy=false)
        =>new(CardKey(m,p,e),m.GameWorldId,m.Id,m.SeasonNumber,m.RoundNumber,e.Minute,e.ClubId,m.CompetitionType,e.EventType,duration,legacy);

    public static void RecordCard(MatchFixture m,Player p,MatchEventRecord e,int seed)
    {
        if(!IsCard(e))throw new ArgumentException("Expected a card event.");
        string key=CardKey(m,p,e);
        if(p.CardHistory.Any(c=>c.Key==key))return;
        if(p.DisciplineVersion==0)
        {
            p.TotalYellowCards=Math.Max(p.TotalYellowCards,Math.Max(p.SeasonYellows-(e.EventType==MatchEventType.DirectRedCard?0:1),p.YellowCardsInCurrentCycle));
            p.DisciplineVersion=Version;
        }
        if(p.DisciplineVersion!=Version)throw new InvalidOperationException("Unsupported player discipline version.");
        if(!m.Discipline.Cards.TryGetValue(p.Id,out var state))
            m.Discipline.Cards[p.Id]=state=new(){CycleBefore=p.YellowCardsInCurrentCycle,BalanceBefore=p.SuspensionMatchesRemaining,ReasonBefore=p.SuspensionReason};
        int duration=0;
        if(e.EventType==MatchEventType.DirectRedCard)
        {
            duration=e.DisciplinarySuspensionMatches??DrawDirectRedDuration(m,p,seed,e.Minute);
            if(duration is <1 or >4)throw new InvalidOperationException("Invalid direct red duration.");
            state.DirectBan=duration;p.TotalDirectRedCards++;
        }
        else
        {
            state.Yellows++;p.TotalYellowCards++;
            if(e.EventType==MatchEventType.SecondYellowRedCard){state.SecondYellowDismissal=true;p.TotalSecondYellowDismissals++;}
        }
        int cycle=state.SecondYellowDismissal?state.CycleBefore:state.CycleBefore+(state.Yellows>0?1:0);
        bool third=!state.SecondYellowDismissal&&cycle>=3;
        var yellowSuspensions=e.ClubId==m.HomeClubId?m.HomeSuspendedByYellowInMatchIds:m.AwaySuspendedByYellowInMatchIds;
        if(third&&!yellowSuspensions.Contains(p.Id))yellowSuspensions.Add(p.Id);
        if(state.SecondYellowDismissal)yellowSuspensions.Remove(p.Id);
        p.YellowCardsInCurrentCycle=third?0:cycle;
        int awarded=Math.Max(state.DirectBan,state.SecondYellowDismissal||third?1:0);
        string reason=state.DirectBan>0?"DIRECT_RED":state.SecondYellowDismissal?"SECOND_YELLOW":third?"THREE_YELLOWS":"";
        p.SuspensionMatchesRemaining=Math.Max(state.BalanceBefore,awarded);
        p.SuspensionReason=awarded>=state.BalanceBefore&&awarded>0?reason:state.ReasonBefore;
        if(awarded>0)
        {
            string awardKey=$"{m.GameWorldId}:M{m.Id}:P{p.Id}:AWARD";
            var history=p.SuspensionHistory.FirstOrDefault(h=>h.Key==awardKey);
            if(history==null){history=new(){Key=awardKey,WorldId=m.GameWorldId,MatchId=m.Id,ClubId=e.ClubId,Season=m.SeasonNumber,Round=m.RoundNumber,Action="AWARD",BalanceBefore=state.BalanceBefore};p.SuspensionHistory.Add(history);}
            history.Minute=e.Minute;history.Matches=awarded;history.Reason=reason;history.BalanceAfter=p.SuspensionMatchesRemaining;
        }
        e.DisciplinarySuspensionMatches=duration>0?duration:awarded;
        p.CardHistory.Add(Card(m,p,e,e.DisciplinarySuspensionMatches??0));
    }

    public static void SettleFinishedMatches(GameWorld w,IEnumerable<MatchFixture> fixtures)
    {
        EnsureMigrated(w);
        var players=w.Players.ToDictionary(p=>p.Id);
        foreach(var m in fixtures.Where(m=>m.IsFinished&&m.CurrentMinute>0&&!m.IsCancelled&&!m.IsPostponed)
            .OrderBy(m=>m.SeasonNumber).ThenBy(m=>m.RoundNumber).ThenBy(m=>m.DeterministicMatchIndex))
        {
            if(w.SettledDisciplineMatches.Contains(m.Id))continue;
            CaptureKickoff(m,players);
            foreach(var e in m.Events.Where(IsCard).OrderBy(e=>e.Minute))
                if(e.PrimaryPlayerId is Guid id&&players.TryGetValue(id,out var p))RecordCard(m,p,e,w.Seed);
            var participants=ClassicForceEvolutionEngine.ParticipationMinutes(m);
            foreach(var s in m.Discipline.SuspendedPlayers)
            {
                if(!players.TryGetValue(s.PlayerId,out var p)||p.ClubId!=s.ClubId||p.SuspensionMatchesRemaining<=0
                    ||participants.GetValueOrDefault(p.Id)>0||m.Discipline.Cards.ContainsKey(p.Id))continue;
                string key=$"{w.Id}:M{m.Id}:P{p.Id}:SERVE";
                if(p.SuspensionHistory.Any(h=>h.Key==key))continue;
                int before=p.SuspensionMatchesRemaining--;
                p.SuspensionHistory.Add(new(){Key=key,WorldId=w.Id,MatchId=m.Id,ClubId=s.ClubId,Season=m.SeasonNumber,Round=m.RoundNumber,
                    Minute=m.CurrentMinute,Action="SERVE",Reason=p.SuspensionReason??"LEGACY_UNSPECIFIED",Matches=1,BalanceBefore=before,BalanceAfter=p.SuspensionMatchesRemaining});
                if(p.SuspensionMatchesRemaining==0)p.SuspensionReason=null;
            }
            w.SettledDisciplineMatches.Add(m.Id);
        }
    }
}
