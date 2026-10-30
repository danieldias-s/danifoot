using System.Buffers.Binary;
using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Owner-authorized adaptation. Not a claim about the exact historical Elifoot formula.</summary>
public static class ClassicForceEvolutionEngine
{
    public const string Rule = "CLASSIC_ADAPTED_FORCE_EVOLUTION";
    public const int Version = 1;
    public const double PlayedDeclineProbability = 0.15;
    private static readonly int[][] Ceilings =
    [
        [50, 50, 50, 49, 48, 47, 46, 44],
        [44, 42, 42, 39, 37, 34, 31, 28],
        [28, 26, 26, 23, 21, 18, 15, 12],
        [12, 10, 10, 8, 6, 4, 2, 1]
    ];
    // Exact probabilities in units of 0.5%, not floating point intervals.
    public static IReadOnlyList<int> DurationWeights { get; } = Array.AsReadOnly(new[] { 26, 26, 24, 24, 22, 20, 18, 12, 8, 6, 5, 4, 3, 2 });
    public static bool IsActive(GameWorld w)
    {
        if (!Enum.IsDefined(w.ForceEvolutionMode) || (w.ForceEvolutionMode == ForceEvolutionMode.ClassicAdapted && w.ClassicForce.Version != Version))
            throw new InvalidOperationException("Unsupported force evolution mode/version; explicit migration is required.");
        return w.ForceEvolutionMode == ForceEvolutionMode.ClassicAdapted;
    }
    public static int GrowthCeiling(DivisionTier division, int position)
        => division is >= DivisionTier.PrimeiraDivisao and <= DivisionTier.QuartaDivisao
            ? Ceilings[(int)division - 1][Math.Clamp(position, 1, 8) - 1] : 1;

    public static double ReserveProbability(int force, int ceiling)
        => force >= ceiling ? 0 : 0.15 + 0.70 * Math.Clamp((ceiling - force - 1.0) / Math.Max(1, ceiling - 2), 0, 1);

    public static int DurationFromRoll(int roll)
    {
        if (roll is < 0 or >= 200) throw new ArgumentOutOfRangeException(nameof(roll));
        int cumulative = 0;
        for (int i = 0; i < DurationWeights.Count; i++)
        {
            cumulative += DurationWeights[i];
            if (roll < cumulative) return i + 2;
        }
        throw new InvalidOperationException("Invalid duration distribution.");
    }

    public static int MaximumLoss(int force, int duration) => Math.Min(force - 1, force * duration / 30);
    public static int ScheduledCumulativeLoss(int total, int duration, int elapsed)
        => total * Math.Clamp(elapsed, 0, duration) / duration;

    public static void InitializeSeason(GameWorld w)
    {
        if (!IsActive(w)) return;
        var state = w.ClassicForce;
        if (state.Version != Version) throw new InvalidOperationException("Unsupported force rules version.");
        if (state.CalendarSeason == w.SeasonNumber) return;
        state.CalendarSeason = w.SeasonNumber;
        state.SeasonStartDate = state.GameDate;
        state.PreparedRound = 0;
        state.CompletedLeaguePositions.Clear();
        state.SeasonStartingStrengths = w.Players.ToDictionary(p => p.Id, p => p.Strength);
        w.PlayerStrengthHistoryStartSeason ??= w.SeasonNumber;
        w.PlayerStrengthHistoryStartRound ??= w.RoundNumber;
    }

    public static DateOnly RoundDate(GameWorld w, int round)
    {
        int gaps = Math.Max(0, round - 1);
        return w.ClassicForce.SeasonStartDate.AddDays(gaps / 2 * 7 + (gaps % 2 == 1 ? 4 : 0));
    }

    public static void PrepareRound(GameWorld w)
    {
        if (!IsActive(w)) return;
        InitializeSeason(w);
        string key = $"S{w.SeasonNumber}:R{w.RoundNumber}";
        var state = w.ClassicForce;
        DateOnly planned;
        if (!state.RoundDates.TryGetValue(key, out planned))
        {
            int offset = RoundDate(w, w.RoundNumber).DayNumber - RoundDate(w, Math.Max(1, state.PreparedRound)).DayNumber;
            planned = state.PreparedRound == 0 ? RoundDate(w,w.RoundNumber) : state.GameDate.AddDays(offset);
        }
        if (planned < state.GameDate) planned = state.GameDate;
        AdvanceToDate(w, planned);
        var participatingClubs = w.Fixtures.Where(m => m.SeasonNumber == w.SeasonNumber && m.RoundNumber == w.RoundNumber && m.CurrentMinute == 0)
            .SelectMany(m => new[] {m.HomeClubId,m.AwayClubId}).Distinct().ToHashSet();
        var squads = w.Players.Where(p => p.ClubId is Guid id && participatingClubs.Contains(id))
            .GroupBy(p => p.ClubId!.Value).ToDictionary(g => g.Key,g => g.ToList());
        List<Guid> Waiting() => participatingClubs.Where(id => !squads.TryGetValue(id,out var squad)
            || squad.Count(p => p.Position == PlayerPosition.G && p.InjuryDaysRemaining == 0) < 1
            || squad.Count(p => p.Position != PlayerPosition.G && p.InjuryDaysRemaining == 0) < 10).Order().ToList();
        var waiting = Waiting();
        for (int day = 0; waiting.Count > 0 && day < 15; day++)
        {
            AdvanceToDate(w,state.GameDate.AddDays(1));
            if (Waiting().Count == 0) break;
        }
        if (Waiting().Count > 0) throw new InvalidOperationException("Club has insufficient roster depth unrelated to a recoverable injury.");
        if (waiting.Count > 0) state.CalendarDelays.Add(new(w.SeasonNumber,w.RoundNumber,planned,state.GameDate,waiting));
        state.RoundDates[key] = state.GameDate;
        state.PreparedRound = w.RoundNumber;
        foreach (var m in w.Fixtures.Where(m => m.SeasonNumber == w.SeasonNumber && m.RoundNumber == w.RoundNumber))
        {
            m.ForceRulesVersion = Version;
            if (m.CurrentMinute == 0) m.GameDate = w.ClassicForce.GameDate;
        }
    }

    public static void AdvanceToDate(GameWorld w, DateOnly target)
    {
        if (!IsActive(w)) return;
        if (target < w.ClassicForce.GameDate) throw new InvalidOperationException("Virtual calendar cannot run backwards.");
        if (target > w.ClassicForce.GameDate && w.Fixtures.Any(m => m.SeasonNumber == w.SeasonNumber && !m.IsFinished && m.CurrentMinute > 0))
            throw new InvalidOperationException("Cannot advance injury days while a match is running.");
        var clubs = w.Clubs.ToDictionary(c => c.Id);
        while (w.ClassicForce.GameDate < target)
        {
            w.ClassicForce.GameDate = w.ClassicForce.GameDate.AddDays(1);
            foreach (var p in w.Players)
            foreach (var injury in p.ForceInjuries.Where(i => i.ProcessedDays < i.Duration))
            {
                int elapsed = Math.Clamp(w.ClassicForce.GameDate.DayNumber - injury.StartDate.DayNumber, 0, injury.Duration);
                while (injury.ProcessedDays < elapsed)
                {
                    int next = injury.ProcessedDays + 1;
                    int loss = ScheduledCumulativeLoss(injury.TotalLoss, injury.Duration, next)
                        - ScheduledCumulativeLoss(injury.TotalLoss, injury.Duration, injury.ProcessedDays);
                    int before = p.Strength;
                    p.Strength = Math.Max(1, before - loss);
                    injury.ProcessedDays = next; // persists even when force 1 prevents an actual loss
                    injury.ActualLoss += before - p.Strength;
                    if (before != p.Strength)
                    {
                        var club = p.ClubId is Guid id ? clubs.GetValueOrDefault(id) : null;
                        var context = ClubContext(w, club);
                        Record(w, p, club, context.Position, context.Ceiling, before, "INJURY_DAILY_LOSS",
                            $"{w.Id}:injury:{injury.Id}:{injury.StartDate.AddDays(next):yyyy-MM-dd}", injury,
                            injury.StartDate.AddDays(next));
                    }
                }
                p.InjuryDaysRemaining = Math.Max(0, injury.EndDate.DayNumber - w.ClassicForce.GameDate.DayNumber);
            }
        }
    }

    public static ForceInjury RegisterInjury(MatchFixture match, Club club, Player p, int worldSeed)
    {
        if (match.ForceRulesVersion != Version || match.GameDate == null) throw new InvalidOperationException("Injury requires a prepared classic fixture.");
        if (p.InjuryDaysRemaining > 0) throw new InvalidOperationException("A player with an active injury cannot sustain a second on-field episode.");
        var rng = DeterministicRng.FromContext(worldSeed, match.SeasonNumber, match.RoundNumber,
            match.DeterministicMatchIndex, match.CurrentMinute, 12031 + PlayerToken(p.Id));
        int duration = DurationFromRoll(rng.NextInt(0, 200));
        int total = rng.NextInt(0, MaximumLoss(p.Strength, duration) + 1);
        var injury = new ForceInjury
        {
            Id = CoachCareerEngine.StableId($"force-injury:{match.GameWorldId}:{match.Id}:{p.Id}:{p.ForceInjuries.Count}"),
            MatchId = match.Id, Season = match.SeasonNumber, Round = match.RoundNumber,
            StartDate = match.GameDate.Value, Duration = duration, InitialForce = p.Strength, TotalLoss = total
        };
        p.ForceInjuries.Add(injury);
        p.InjuryDaysRemaining = duration;
        return injury;
    }

    public static void CaptureRoster(MatchFixture match, IReadOnlyDictionary<Guid, Player> players)
    {
        if (match.ForceRulesVersion != Version || match.ForceRoster.Count > 0) return;
        match.ForceRoster = players.Values.Where(p => p.ClubId == match.HomeClubId || p.ClubId == match.AwayClubId)
            .Select(p => new ForceRosterPlayer(p.Id, p.ClubId!.Value)).OrderBy(p => p.PlayerId).ToList();
    }

    public static Dictionary<Guid, int> ParticipationMinutes(MatchFixture m)
    {
        var minutes = new Dictionary<Guid, int>();
        var active = m.StatisticsInitialLineup.Where(p => p.IsStarter).ToDictionary(p => p.PlayerId, _ => 0);
        int end = Math.Max(0, m.CurrentMinute);
        void Remove(Guid? id, int minute)
        {
            if (id is Guid player && active.Remove(player, out int start))
                minutes[player] = minutes.GetValueOrDefault(player) + Math.Max(0, minute - start);
        }
        foreach (var e in m.Events.OrderBy(e => e.Minute))
        {
            int minute = Math.Clamp(e.Minute, 0, end);
            if (e.EventType == MatchEventType.Substitution)
            {
                Remove(e.SecondaryPlayerId, minute);
                if (e.PrimaryPlayerId is Guid incoming) active.TryAdd(incoming, minute);
            }
            else if (e.EventType is MatchEventType.DirectRedCard or MatchEventType.SecondYellowRedCard
                || (m.ForceRulesVersion == Version && e.EventType == MatchEventType.Injury)) Remove(e.PrimaryPlayerId, minute);
        }
        foreach (var (id, start) in active) minutes[id] = minutes.GetValueOrDefault(id) + Math.Max(0, end - start);
        return minutes;
    }

    public static void ApplyFinishedRound(GameWorld w, IReadOnlyList<MatchFixture> matches)
    {
        if (!IsActive(w) || matches.Any(m => !m.IsFinished)) return;
        if (matches.Any(m => m.SeasonNumber != w.SeasonNumber || m.RoundNumber != w.RoundNumber))
            throw new InvalidOperationException("Force processing requires the current sporting round.");
        if (w.ClassicForce.GuardSeason != w.SeasonNumber || w.ClassicForce.GuardRound != w.RoundNumber)
        {
            w.ClassicForce.ProcessedRoundPlayers.Clear();
            w.ClassicForce.GuardSeason = w.SeasonNumber;
            w.ClassicForce.GuardRound = w.RoundNumber;
        }
        var players = w.Players.ToDictionary(p => p.Id);
        var clubs = w.Clubs.ToDictionary(c => c.Id);
        var played = matches.ToDictionary(m => m.Id, ParticipationMinutes);
        var league = matches.Any(m => m.CompetitionType == CompetitionType.League && m.CurrentMinute > 0);
        if (league)
            foreach (var tier in Ceilings.Select((_, i) => (DivisionTier)(i + 1)))
                foreach (var row in CompetitionAndSeasonEngine.GetDivisionStandings(w, tier))
                    w.ClassicForce.CompletedLeaguePositions[row.ClubId] = row.Position;

        // If ownership changed during the round, an actual appearance takes precedence over a reserve slot.
        var candidates = matches.Where(m => m.CurrentMinute > 0 && !w.ClassicForce.ProcessedMatches.Contains(m.Id))
            .SelectMany(m => m.ForceRoster.Select(p => (Match: m, Roster: p, Minutes: played[m.Id].GetValueOrDefault(p.PlayerId))))
            .OrderByDescending(x => x.Minutes > 0).ThenBy(x => x.Match.DeterministicMatchIndex).ThenBy(x => x.Roster.PlayerId);
        foreach (var x in candidates)
        {
            if (!players.TryGetValue(x.Roster.PlayerId, out var p) || !clubs.TryGetValue(x.Roster.ClubId, out var club)) continue;
            string roundKey = $"{w.Id}:S{w.SeasonNumber}:R{w.RoundNumber}:P{p.Id}";
            if (!w.ClassicForce.ProcessedRoundPlayers.Add(roundKey)) continue;
            var context = ClubContext(w, club);
            string reason = x.Minutes > 0 ? "PLAYED_MATCH_DECLINE" : "RESERVE_PROGRESS";
            string key = $"{roundKey}:M{x.Match.Id}:{reason}";
            int before = p.Strength;
            var rng = DeterministicRng.FromContext(w.Seed, w.SeasonNumber, w.RoundNumber,
                x.Match.DeterministicMatchIndex, PlayerToken(p.Id), x.Minutes > 0 ? 12042 : 12041);
            bool injuredInMatch = x.Match.HomeInjuredInMatchIds.Contains(p.Id) || x.Match.AwayInjuredInMatchIds.Contains(p.Id);
            if (x.Minutes > 0)
            {
                if (!injuredInMatch && p.Strength > 1 && rng.NextBool(PlayedDeclineProbability)) p.Strength--;
            }
            else if (x.Match.CompetitionType == CompetitionType.League && club.DivisionTier != DivisionTier.Distrital
                && p.Strength < context.Ceiling && rng.NextBool(ReserveProbability(p.Strength, context.Ceiling))) p.Strength++;
            if (before != p.Strength) Record(w, p, club, context.Position, context.Ceiling, before, reason, key);
        }
        foreach (var m in matches) w.ClassicForce.ProcessedMatches.Add(m.Id);
        if (league) CaptureTurns(w);
    }

    public static (int Position, int Ceiling) ClubContext(GameWorld w, Club? club)
    {
        int rank = club == null ? 4 : w.ClassicForce.CompletedLeaguePositions.GetValueOrDefault(club.Id, 4);
        return (rank, club == null ? 1 : GrowthCeiling(club.DivisionTier, rank));
    }

    private static void CaptureTurns(GameWorld w)
    {
        foreach (var club in w.Clubs.Where(c => c.DivisionTier != DivisionTier.Distrital))
        {
            int half = club.LeaguePlayed == 7 ? 1 : club.LeaguePlayed == 14 ? 2 : 0;
            if (half == 0 || w.ClassicForce.TurnSnapshots.Any(s => s.Season == w.SeasonNumber && s.Half == half && s.ClubId == club.Id)) continue;
            var context = ClubContext(w, club);
            var changes = w.PlayerStrengthHistory.Where(e => e.Season == w.SeasonNumber && e.ClubId == club.Id && e.Rule == Rule).ToList();
            var roster = w.Players.Where(p => p.ClubId == club.Id).Select(p =>
            {
                var events = changes.Where(e => e.PlayerId == p.Id).ToList();
                return new ForceTurnPlayer(p.Id, w.ClassicForce.SeasonStartingStrengths.GetValueOrDefault(p.Id, p.Strength), p.Strength,
                    events.Sum(e => e.ReserveGrowth), events.Where(e => e.ChangeReason == "PLAYED_MATCH_DECLINE").Sum(e => e.StrengthBefore - e.StrengthAfter), events.Sum(e => e.DailyInjuryLoss));
            }).ToList();
            w.ClassicForce.TurnSnapshots.Add(new(w.SeasonNumber, half, w.ClassicForce.GameDate, club.Id, club.DivisionTier,
                context.Position, context.Ceiling, roster, changes.Sum(e => e.ReserveGrowth),
                changes.Where(e => e.ChangeReason == "PLAYED_MATCH_DECLINE").Sum(e => e.StrengthBefore - e.StrengthAfter), changes.Sum(e => e.DailyInjuryLoss)));
        }
    }

    private static void Record(GameWorld w, Player p, Club? club, int rank, int ceiling, int before, string reason,
        string key, ForceInjury? injury = null, DateOnly? date = null)
    {
        p.MarketPrice = MarketAndEconomyEngine.CalculateFairValue(p);
        p.SalaryPerRound = MarketAndEconomyEngine.CalculateFairSalaryPerRound(p);
        w.PlayerStrengthHistory.Add(new(key, p.Id, w.SeasonNumber, w.RoundNumber, club?.Id, club?.Name ?? "Sem clube",
            club?.DivisionTier, before, p.Strength, reason, injury?.Id, Rule, reason)
        {
            WorldId = w.Id, GameDate = date ?? w.ClassicForce.GameDate, LeaguePosition = rank, GrowthCeiling = ceiling,
            InjuryDuration = injury?.Duration, DailyInjuryLoss = reason == "INJURY_DAILY_LOSS" ? before - p.Strength : 0,
            ReserveGrowth = reason == "RESERVE_PROGRESS" ? p.Strength - before : 0
        });
    }

    private static int PlayerToken(Guid id) => BinaryPrimitives.ReadInt32LittleEndian(id.ToByteArray()) & 0x3fffffff;
}
