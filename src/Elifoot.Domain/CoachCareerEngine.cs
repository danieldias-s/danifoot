using System.Security.Cryptography;
using System.Text;
using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class CoachCareerEngine
{
    public static bool IsSafe(GameWorld w) => w.RoundState is RoundState.PREPARING or RoundState.RESULTS or RoundState.FINISHED;
    public static int Ordinal(GameWorld w) => (w.SeasonNumber - 1) * w.TotalRoundsInSeason + w.RoundNumber;
    public static Guid StableId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));

    public sealed class Coach
    {
        public HumanCoach? Human { get; }
        public AiManager? Ai { get; }
        public Coach(HumanCoach h) => Human = h;
        public Coach(AiManager a) => Ai = a;
        public Guid Id => Human?.Id ?? Ai!.Id;
        public string Name => Human?.Nickname ?? Ai!.Name;
        public Guid? ClubId { get => Human?.CurrentClubId ?? Ai?.CurrentClubId; set { if (Human != null) Human.CurrentClubId = value; else Ai!.CurrentClubId = value; } }
        public CoachCareerState State => Human?.Career ?? Ai!.Career;
        public List<CoachEmploymentRecord> Employment => Human?.EmploymentHistory ?? Ai!.EmploymentHistory;
        public decimal Points { get => Human?.CoachPoints ?? Ai!.CoachPoints; set { if (Human != null) Human.CoachPoints = value; else Ai!.CoachPoints = value; } }
        public int LeagueTitles { get => Human?.LeagueTitles ?? Ai!.LeagueTitles; set { if (Human != null) Human.LeagueTitles = value; else Ai!.LeagueTitles = value; } }
        public int CupTitles { get => Human?.CupTitles ?? Ai!.CupTitles; set { if (Human != null) Human.CupTitles = value; else Ai!.CupTitles = value; } }
        public int Wins => Human?.TotalWins ?? Ai!.TotalWins;
        public void Result(int result) { if (Human != null) { if (result > 0) Human.TotalWins++; else if (result == 0) Human.TotalDraws++; else Human.TotalLosses++; } else { if (result > 0) Ai!.TotalWins++; else if (result == 0) Ai!.TotalDraws++; else Ai!.TotalLosses++; } }
        public void Dismissal() { if (Human != null) Human.Dismissals++; else Ai!.Dismissals++; }
    }

    public static IEnumerable<Coach> Coaches(GameWorld w) => w.HumanCoaches.Select(h => new Coach(h)).Concat(w.AiManagers.Select(a => new Coach(a)));
    public static Coach? Find(GameWorld w, Guid? id) => id == null ? null : Coaches(w).FirstOrDefault(c => c.Id == id);

    public static void EnsureMigrated(GameWorld w, bool isNewWorld = false)
    {
        if (w.CareerRulesVersion >= 8) return;
        // Preserve all legacy aggregates. Historical missing points/morale/title owners are not invented.
        foreach (var m in w.Fixtures.Where(f => f.IsFinished)) m.CareerResultsApplied = true;
        foreach (var c in Coaches(w))
        {
            c.State.Morale = Math.Clamp(c.State.Morale, 0, 100);
            if (c.ClubId is Guid id && c.Employment.Count == 0)
                c.Employment.Add(new CoachEmploymentRecord { ClubId = id, ClubName = w.Clubs.FirstOrDefault(x => x.Id == id)?.Name ?? "Clube legado", StartSeason = w.SeasonNumber, StartRound = w.RoundNumber, ReasonForLeaving = null });
        }
        foreach (var club in w.Clubs.Where(c => c.HumanCoachId != null && c.AiManagerId != null))
        {
            var ai = w.AiManagers.FirstOrDefault(a => a.Id == club.AiManagerId);
            if (ai?.CurrentClubId == club.Id) ai.CurrentClubId = null;
            club.AiManagerId = null;
        }
        w.CareerRulesVersion = 8;
        foreach (var invitation in w.JobInvitations.Where(i => !i.IsAccepted && !i.IsExpired && !i.IsRejected && i.VacancyId == Guid.Empty))
        {
            var club = w.Clubs.FirstOrDefault(c => c.Id == invitation.ClubId);
            if (club == null || club.HumanCoachId != null || club.DivisionTier == DivisionTier.Distrital) { invitation.IsExpired = true; continue; }
            OpenVacancy(w, club);
            invitation.VacancyId = club.CareerVacancyId!.Value;
            invitation.ExpiresAtOrdinal = Ordinal(w) + 3;
            invitation.Explanation = "MIGRATED_LEGACY_INVITATION;CANDIDATE_GOOD_MORALE;validar elegibilidade no aceite";
        }
        foreach (var club in w.Clubs.Where(c => c.DivisionTier == DivisionTier.Distrital && c.HumanCoachId != null).ToList())
            Dismiss(w, club.HumanCoachId!.Value, club, "LEGACY_DISTRICT_DISMISSAL");
        Log(w, isNewWorld ? "CAREER_INITIALIZED" : "CAREER_MIGRATED", null, null, isNewWorld ? "008: nova carreira, moral70." : "008: agregados legados preservados; partidas antigas marcadas sem reprocessar; moral inicial70, histórico anterior desconhecido.");
    }

    public static void CaptureLeadership(GameWorld w, MatchFixture m)
    {
        EnsureMigrated(w);
        if (m.CareerLeadershipCaptured) return;
        var home = w.Clubs.Single(c => c.Id == m.HomeClubId); var away = w.Clubs.Single(c => c.Id == m.AwayClubId);
        m.HomeCoachAtKickoffId = home.HumanCoachId ?? home.AiManagerId;
        m.AwayCoachAtKickoffId = away.HumanCoachId ?? away.AiManagerId;
        m.HomeMoraleAtKickoff = Find(w, m.HomeCoachAtKickoffId)?.State.Morale ?? 70;
        m.AwayMoraleAtKickoff = Find(w, m.AwayCoachAtKickoffId)?.State.Morale ?? 70;
        m.CareerLeadershipCaptured = true;
    }

    public static decimal LeaguePoints(DivisionTier division, bool win, bool draw)
        => division == DivisionTier.Distrital ? 0m : win ? 5 - (int)division : draw ? (5 - (int)division) / 2m : 0m;
    public static decimal CupPhasePoints(CompetitionType phase) => phase switch
    {
        CompetitionType.CupRoundOf64 => 3, CompetitionType.CupRoundOf32 => 4,
        CompetitionType.CupRoundOf16 => 5, CompetitionType.CupQuarterFinal => 6,
        CompetitionType.CupSemiFinal => 7, CompetitionType.CupFinal => 8, _ => 0
    };
    public static double Strength(GameWorld w, Club club) => w.Players.Where(p => p.ClubId == club.Id).Select(p => (double)p.Strength).DefaultIfEmpty(20).Average();
    public static int TablePosition(GameWorld w, Club club) => CompetitionAndSeasonEngine.GetDivisionStandings(w, club.DivisionTier).FindIndex(r => r.ClubId == club.Id) + 1;

    public static void ApplyMatchCareer(GameWorld w, MatchFixture m, Club home, Club away)
    {
        CaptureLeadership(w, m);
        bool knockout = m.CompetitionType != CompetitionType.League;
        if (knockout && m.WinnerClubId != home.Id && m.WinnerClubId != away.Id) return;
        int homeResult = knockout && m.WinnerClubId.HasValue ? m.WinnerClubId == home.Id ? 1 : -1 : Math.Sign(m.HomeGoals - m.AwayGoals);
        Apply(m.HomeCoachAtKickoffId, home, away, homeResult, m.HomeMoraleAtKickoff, m.HomeGoals - m.AwayGoals);
        Apply(m.AwayCoachAtKickoffId, away, home, -homeResult, m.AwayMoraleAtKickoff, m.AwayGoals - m.HomeGoals);
        if (m.CompetitionType == CompetitionType.CupFinal && m.WinnerClubId is Guid winner)
            AwardTitle(w, winner == home.Id ? m.HomeCoachAtKickoffId : m.AwayCoachAtKickoffId, winner, null, "CUP", m.SeasonNumber);

        void Apply(Guid? id, Club club, Club opponent, int result, int kickoffMorale, int goalDiff)
        {
            var coach = Find(w, id); if (coach == null) return;
            decimal pts = knockout ? result > 0 ? CupPhasePoints(m.CompetitionType) : 0m : LeaguePoints(m.DivisionTier ?? club.DivisionTier, result > 0, result == 0);
            coach.Points += pts; coach.Result(result);
            var s = coach.State; int before = s.Morale;
            double gap = Strength(w, club) - Strength(w, opponent);
            bool favorite = gap >= 6 || club.Reputation >= opponent.Reputation + 15;
            bool underdog = gap <= -6 || club.Reputation + 15 <= opponent.Reputation;
            int delta = result > 0 ? favorite ? 6 : underdog ? 13 : 9 : result == 0 ? favorite ? -3 : underdog ? 4 : 1 : favorite ? -14 : underdog ? -4 : -9;
            s.WinlessMatches = result > 0 ? 0 : s.WinlessMatches + 1;
            s.RecentResults.Add(result); if (s.RecentResults.Count > 5) s.RecentResults.RemoveAt(0);
            if (result <= 0) delta -= Math.Min(4, Math.Max(0, s.WinlessMatches - 2));
            if (result < 0 && goalDiff <= -3) delta -= 2;
            if (club.DivisionTier != DivisionTier.Distrital && club.LeaguePlayed >= 3)
            {
                var expected = w.Clubs.Where(c => c.DivisionTier == club.DivisionTier).OrderByDescending(c => Strength(w, c) + c.Reputation * .15).ThenBy(c => c.DeterministicIndex).ToList().FindIndex(c => c.Id == club.Id) + 1;
                var actual = TablePosition(w, club);
                if (result <= 0 && actual > expected + 2) delta -= 2;
                else if (result >= 0 && actual + 2 < expected) delta++;
            }
            s.Morale = Math.Clamp(before + delta, 0, 100);
            s.MoraleHistory.Add(new(m.SeasonNumber, m.RoundNumber, m.Id, before, s.Morale, $"RESULT_{result};expectedGap={gap:F1};winless={s.WinlessMatches};recent5={s.RecentResults.Sum()};points={pts}"));
            Log(w, "COACH_MATCH_POINTS", coach.Id, club.Id, $"{m.Id}: +{pts}; moral{before}->{s.Morale}");
            if (kickoffMorale < w.CoachMoraleRules.YellowMinimum && result < 0)
                Dismiss(w, coach.Id, club, "RED_MORALE_LOSS");
        }
    }

    public static void AwardTitle(GameWorld w, Guid? coachId, Guid clubId, DivisionTier? division, string type, int season)
    {
        if (w.CoachTitles.Any(t => t.Season == season && t.Division == division && t.TitleType == type)) return;
        var c = Find(w, coachId); if (c == null) return;
        w.CoachTitles.Add(new(season, division, clubId, c.Id, type));
        if (type == "LEAGUE") c.LeagueTitles++; else c.CupTitles++;
        Log(w, "COACH_TITLE", c.Id, clubId, $"{type}:{division}:S{season}");
    }

    public static void AwardCompletedLeagues(GameWorld w)
    {
        foreach (var tier in new[] { DivisionTier.PrimeiraDivisao, DivisionTier.SegundaDivisao, DivisionTier.TerceiraDivisao, DivisionTier.QuartaDivisao })
        {
            var games = w.Fixtures.Where(f => f.SeasonNumber == w.SeasonNumber && f.CompetitionType == CompetitionType.League && f.DivisionTier == tier).ToList();
            if (games.Count == 0 || games.Any(f => !f.IsFinished || !f.CareerResultsApplied)) continue;
            var winner = CompetitionAndSeasonEngine.GetDivisionStandings(w, tier).First();
            var last = games.Where(g => g.HomeClubId == winner.ClubId || g.AwayClubId == winner.ClubId).OrderByDescending(g => g.RoundNumber).ThenByDescending(g => g.DeterministicMatchIndex).First();
            AwardTitle(w, last.HomeClubId == winner.ClubId ? last.HomeCoachAtKickoffId : last.AwayCoachAtKickoffId, winner.ClubId, tier, "LEAGUE", w.SeasonNumber);
        }
    }

    public static bool Dismiss(GameWorld w, Guid id, Club club, string reason)
    {
        var coach = Find(w, id);
        if (coach == null || coach.ClubId != club.Id || (club.HumanCoachId != id && club.AiManagerId != id)) return false;
        coach.Dismissal(); Release(w, coach, club, reason);
        coach.State.UnemployedSinceOrdinal = Ordinal(w);
        Log(w, "COACH_DISMISSED", id, club.Id, reason);
        OpenVacancy(w, club); EnsureProvisional(w, club);
        return true;
    }

    private static void Release(GameWorld w, Coach c, Club club, string reason)
    {
        var employment = c.Employment.LastOrDefault(e => e.ClubId == club.Id && e.EndSeason == null);
        if (employment != null) { employment.EndSeason = w.SeasonNumber; employment.EndRound = w.RoundNumber; employment.ReasonForLeaving = reason; }
        if (club.HumanCoachId == c.Id) club.HumanCoachId = null;
        if (club.AiManagerId == c.Id) club.AiManagerId = null;
        c.ClubId = null;
        if (c.Human != null) { c.Human.IsReadyForRound = false; c.Human.SubmittedStarterIds.Clear(); c.Human.SubmittedBenchIds.Clear(); c.Human.SubmittedFormation = null; }
    }

    public static void OpenVacancy(GameWorld w, Club club)
    {
        if (club.CareerVacancyId.HasValue) return;
        club.CareerVacancySequence++;
        club.CareerVacancyId = StableId($"vacancy:{w.Seed}:{club.DeterministicIndex}:{Ordinal(w)}:{club.CareerVacancySequence}");
        club.LastVacancySearchOrdinal = -1;
    }

    public static void EnsureProvisional(GameWorld w, Club club)
    {
        if (club.HumanCoachId.HasValue || club.AiManagerId.HasValue) return;
        var free = w.AiManagers.Where(a => a.CurrentClubId == null && a.Career.Morale >= w.CoachMoraleRules.GreenMinimum
                && (a.Career.UnemployedSinceOrdinal < 0 || Ordinal(w) - a.Career.UnemployedSinceOrdinal >= 2))
            .OrderBy(a => a.Id).FirstOrDefault();
        if (free == null)
        {
            free = new AiManager { Id = StableId($"provisional:{w.Seed}:{club.DeterministicIndex}:{Ordinal(w)}:{w.AiManagers.Count}"), GameWorldId = w.Id,
                Name = $"Interino {club.ShortName} {w.AiManagers.Count + 1}", Career = new() { Morale = w.CoachMoraleRules.GreenMinimum }, PreferredFormations = AiManagerBrain.GetDefaultPreferencesForIdentity(ManagerTacticalIdentity.Equilibrado) };
            w.AiManagers.Add(free);
        }
        free.CurrentClubId = club.Id; club.AiManagerId = free.Id; club.HasProvisionalManager = true;
        free.Career.LastMoveOrdinal = Ordinal(w);
        free.EmploymentHistory.Add(new CoachEmploymentRecord { ClubId = club.Id, ClubName = club.Name, StartSeason = w.SeasonNumber, StartRound = w.RoundNumber });
        Log(w, "PROVISIONAL_APPOINTED", free.Id, club.Id, "Partidas garantidas enquanto a vaga é avaliada.");
    }

    public static decimal ProjectScore(GameWorld w, Club c) => Math.Round((decimal)(
        (5 - (int)c.DivisionTier) * 12 + c.Reputation * .30 + Strength(w, c) * .60
        + Math.Log10((double)Math.Max(1m, c.CashBalance)) * 3 + c.Stadium.BuiltCapacity / 10000.0 * 2), 3);

    public static (bool Eligible, decimal Improvement, string Reason) EvaluateOpportunity(GameWorld w, Coach c, Club target)
    {
        if (w.CoachMoraleRules.Band(c.State.Morale) != "VERDE" || c.ClubId == target.Id || (c.Human != null && target.DivisionTier == DivisionTier.Distrital))
            return (false, 0, "CANDIDATE_NOT_ELIGIBLE");
        if (c.ClubId is not Guid currentId) {
            if (c.State.UnemployedSinceOrdinal >= 0 && Ordinal(w) - c.State.UnemployedSinceOrdinal < 2)
                return (false, 0, "CANDIDATE_NOT_ELIGIBLE;DISMISSAL_COOLDOWN");
            bool established = c.LeagueTitles > 0 || c.Points >= 60 || c.Wins >= 15;
            bool compatible = established || target.DivisionTier >= DivisionTier.TerceiraDivisao;
            return (compatible, ProjectScore(w, target), compatible ? "DIVISION_COMPATIBLE;CANDIDATE_GOOD_MORALE" : "CANDIDATE_NOT_ELIGIBLE;EXPERIENCE_FOR_DIVISION");
        }
        var current = w.Clubs.Single(x => x.Id == currentId);
        decimal gain = ProjectScore(w, target) - ProjectScore(w, current);
        if (Ordinal(w) - c.State.LastMoveOrdinal < 3) return (false, gain, "CANDIDATE_NOT_ELIGIBLE;MOVE_COOLDOWN");
        bool leadingElite = current.DivisionTier == DivisionTier.PrimeiraDivisao && current.LeaguePlayed > 0 && TablePosition(w, current) == 1 && current.Reputation >= 75;
        decimal required = leadingElite ? 16 : 6 + (c.Ai?.Pragmatism ?? 50) / 25m;
        bool structuralUpgrade = target.Reputation >= current.Reputation + 15 && target.Stadium.BuiltCapacity >= current.Stadium.BuiltCapacity * 1.3
            && target.CashBalance >= current.CashBalance * 1.3m;
        bool attractive = gain >= required && ((int)target.DivisionTier < (int)current.DivisionTier || structuralUpgrade);
        return (attractive, gain, attractive ? "CLUB_PROJECT_UPGRADE;DIVISION_COMPATIBLE;CANDIDATE_GOOD_MORALE" : "CANDIDATE_REJECTED;PROJECT_NOT_UPGRADE");
    }

    public static void RecoverUnemployed(GameWorld w)
    {
        int ordinal = Ordinal(w);
        foreach (var c in Coaches(w).Where(c => c.ClubId == null))
        {
            var s = c.State;
            if (s.UnemployedSinceOrdinal < 0) s.UnemployedSinceOrdinal = ordinal;
            if (ordinal <= s.UnemployedSinceOrdinal || s.LastRecoveryOrdinal >= ordinal) continue;
            s.LastRecoveryOrdinal = ordinal;
            int before = s.Morale; s.Morale = Math.Min(w.CoachMoraleRules.GreenMinimum, s.Morale + w.CoachMoraleRules.UnemployedRecoveryPerRound);
            if (before > w.CoachMoraleRules.GreenMinimum) s.Morale = before;
            if (s.Morale != before) s.MoraleHistory.Add(new(w.SeasonNumber, w.RoundNumber, null, before, s.Morale, "UNEMPLOYED_RECOVERY"));
        }
    }

    public static void ProcessVacancies(GameWorld w)
    {
        EnsureMigrated(w); if (!IsSafe(w)) return;
        var ranks = CompetitionAndSeasonEngine.GetClassicCoachRanking(w).ToDictionary(r => r.CoachId, r => r.Rank);
        foreach (var club in w.Clubs.Where(c => c.HumanCoachId == null && c.CareerVacancyId.HasValue).OrderBy(c => c.DeterministicIndex).ToList())
        {
            EnsureProvisional(w, club);
            var pending = w.JobInvitations.FirstOrDefault(i => i.VacancyId == club.CareerVacancyId && !i.IsAccepted && !i.IsExpired && !i.IsRejected);
            if (pending != null)
            {
                var candidate = Find(w, pending.CoachId);
                if (pending.ExpiresAtOrdinal > Ordinal(w) && candidate != null && EvaluateOpportunity(w, candidate, club).Eligible) continue;
                pending.IsExpired = true;
                Log(w, "CANDIDATE_NOT_ELIGIBLE", pending.CoachId, club.Id, "Convite expirado ou moral/projeto mudou.");
            }
            if (club.LastVacancySearchOrdinal == Ordinal(w)) continue;
            club.LastVacancySearchOrdinal = Ordinal(w);
            var tried = w.JobInvitations.Where(i => i.VacancyId == club.CareerVacancyId).Select(i => i.CoachId).ToHashSet();
            var candidates = Coaches(w).Where(c => c.ClubId != club.Id && !tried.Contains(c.Id) && c.State.Morale >= w.CoachMoraleRules.GreenMinimum)
                .Select(c => new { Coach = c, Score = (ranks.Count - ranks.GetValueOrDefault(c.Id, ranks.Count)) * 20m / Math.Max(1, ranks.Count)
                    + c.State.Morale / 10m + (c.LeagueTitles > 0 ? 4 : 0) + Math.Min(10, c.Points / 50m)
                    - (c.ClubId is Guid id ? Math.Abs((int)w.Clubs.Single(x => x.Id == id).DivisionTier - (int)club.DivisionTier) * 3 : 0) })
                .OrderByDescending(x => x.Score).ThenBy(x => x.Coach.Id).ToList();
            foreach (var candidate in candidates)
            {
                var c = candidate.Coach; var evaluation = EvaluateOpportunity(w, c, club);
                if (c.Human != null && !evaluation.Eligible) continue;
                if (c.Human != null && w.JobInvitations.Count(i => i.CoachId == c.Id && !i.IsAccepted && !i.IsExpired && !i.IsRejected) >= 3) continue;
                var invitation = new JobInvitation { Id = StableId($"invitation:{club.CareerVacancyId}:{c.Id}"), GameWorldId = w.Id,
                    ClubId = club.Id, CoachId = c.Id, VacancyId = club.CareerVacancyId!.Value, IsAiCandidate = c.Ai != null,
                    CreatedAtSeason = w.SeasonNumber, CreatedAtRound = w.RoundNumber, ExpiresAtOrdinal = Ordinal(w) + 3,
                    Explanation = $"CANDIDATE_HIGH_RANK={ranks.GetValueOrDefault(c.Id)};{evaluation.Reason};project={ProjectScore(w, club)};improvement={evaluation.Improvement}",
                    CandidateScore = candidate.Score, ProjectImprovement = evaluation.Improvement };
                w.JobInvitations.Add(invitation);
                if (c.Human != null) break; // The human decides; provisional manager keeps playing.
                if (!evaluation.Eligible) { invitation.IsRejected = true; Log(w, "CANDIDATE_REJECTED", c.Id, club.Id, evaluation.Reason); continue; }
                Hire(w, c, club, invitation); break;
            }
        }
    }

    public static (bool Success, string? Error) Respond(GameWorld w, Guid coachId, Guid invitationId, bool accept)
    {
        EnsureMigrated(w);
        if (!IsSafe(w)) return (false, "Trocas somente entre partidas.");
        var c = Find(w, coachId); var invitation = w.JobInvitations.FirstOrDefault(i => i.Id == invitationId && i.CoachId == coachId);
        if (c == null || invitation == null || invitation.IsExpired || invitation.IsRejected || invitation.IsAccepted) return (false, "Convite encerrado ou inválido.");
        var club = w.Clubs.FirstOrDefault(x => x.Id == invitation.ClubId);
        if (club == null || club.HumanCoachId.HasValue || (c.Human != null && club.DivisionTier == DivisionTier.Distrital)) return (false, "Clube indisponível.");
        if (invitation.ExpiresAtOrdinal <= Ordinal(w)) { invitation.IsExpired = true; return (false, "Convite expirado."); }
        if (club.CareerVacancyId != invitation.VacancyId) return (false, "Vaga encerrada.");
        if (accept && !EvaluateOpportunity(w, c, club).Eligible) return (false, "Moral deve estar VERDE e o projeto deve continuar elegível.");
        if (accept) Hire(w, c, club, invitation);
        else { invitation.IsRejected = true; Log(w, "CANDIDATE_REJECTED", c.Id, club.Id, "Recusa humana"); club.LastVacancySearchOrdinal = -1; ProcessVacancies(w); }
        return (true, null);
    }

    private static void Hire(GameWorld w, Coach c, Club club, JobInvitation invitation)
    {
        Club? previous = c.ClubId is Guid id ? w.Clubs.First(x => x.Id == id) : null;
        if (previous != null) Release(w, c, previous, "CLUB_PROJECT_UPGRADE");
        var incumbent = Find(w, club.AiManagerId);
        if (incumbent != null) { if (!club.HasProvisionalManager) incumbent.Dismissal(); Release(w, incumbent, club, "NEW_COACH_APPOINTED"); incumbent.State.UnemployedSinceOrdinal = Ordinal(w); }
        club.HumanCoachId = c.Human?.Id; club.AiManagerId = c.Ai?.Id; c.ClubId = club.Id;
        c.State.LastMoveOrdinal = Ordinal(w); c.State.WinlessMatches = 0; c.State.RecentResults.Clear();
        c.Employment.Add(new CoachEmploymentRecord { ClubId = club.Id, ClubName = club.Name, StartSeason = w.SeasonNumber, StartRound = w.RoundNumber });
        if (c.Human != null) c.Human.IsReadyForRound = false;
        invitation.IsAccepted = true;
        foreach (var other in w.JobInvitations.Where(i => !i.IsAccepted && (i.VacancyId == invitation.VacancyId || i.CoachId == c.Id))) other.IsExpired = true;
        club.CareerVacancyId = null; club.HasProvisionalManager = false;
        Log(w, "CANDIDATE_ACCEPTED", c.Id, club.Id, invitation.Explanation);
        if (previous != null) { OpenVacancy(w, previous); EnsureProvisional(w, previous); }
    }

    public static void Log(GameWorld w, string action, Guid? coach, Guid? club, string description)
        => w.AuditLogs.Add(new AuditLogEntry { GameWorldId = w.Id, SeasonNumber = w.SeasonNumber, RoundNumber = w.RoundNumber, ActorCoachId = coach, ClubId = club, ActionType = action, Description = description });
}
