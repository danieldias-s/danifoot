using System.Buffers.Binary;
using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Owner-authorized adaptation. Not a claim about the exact historical Elifoot formula.</summary>
public static class ClassicForceEvolutionEngine
{
    public const string Rule = "CLASSIC_ADAPTED_FORCE_EVOLUTION";
    public const int Version = 1;
    public const double PlayedDeclineProbability = 0.50;
    // Exact probabilities in units of 0.5%, not floating point intervals.
    public static IReadOnlyList<int> DurationWeights { get; } = Array.AsReadOnly(new[] { 26, 26, 24, 24, 22, 20, 18, 12, 8, 6, 5, 4, 3, 2 });
    public static bool IsActive(GameWorld w)
    {
        if (!Enum.IsDefined(w.ForceEvolutionMode) || (w.ForceEvolutionMode == ForceEvolutionMode.ClassicAdapted && w.ClassicForce.Version != Version))
            throw new InvalidOperationException("Unsupported force evolution mode/version; explicit migration is required.");
        return w.ForceEvolutionMode == ForceEvolutionMode.ClassicAdapted;
    }
    public static int GrowthCeiling(DivisionTier division, int position)
        => ChampionshipStrengthLimits.For(division, position);

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
        bool classicInjuries = IsActive(w);
        if (matches.Count == 0 || matches.Any(m => !m.IsFinished)) return;
        if (matches.Any(m => m.SeasonNumber != w.SeasonNumber || m.RoundNumber != w.RoundNumber))
            throw new InvalidOperationException("Force processing requires the current sporting round.");
        var state = w.ClassicForce;
        if (state.GuardSeason > w.SeasonNumber || (state.GuardSeason == w.SeasonNumber && state.GuardRound >= w.RoundNumber))
            return;

        var playedMatches = matches.Where(m => m.CompetitionType == CompetitionType.League && m.CurrentMinute > 0).ToList();
        if (playedMatches.Count == 0) return;
        var clubs = w.Clubs.ToDictionary(c => c.Id);
        foreach (var tier in w.Clubs.Where(c => c.DivisionTier != DivisionTier.Distrital && c.LeaguePlayed > 0)
                     .Select(c => c.DivisionTier).Distinct())
            foreach (var row in CompetitionAndSeasonEngine.GetDivisionStandings(w, tier))
                state.CompletedLeaguePositions[row.ClubId] = row.Position;

        var appeared = new HashSet<Guid>();
        var injured = new HashSet<Guid>();
        foreach (var match in playedMatches)
        {
            appeared.UnionWith(match.StatisticsInitialLineup.Where(p => p.IsStarter).Select(p => p.PlayerId));
            appeared.UnionWith(match.Events.Where(e => e.EventType == MatchEventType.Substitution)
                .Select(e => e.PrimaryPlayerId).OfType<Guid>());
            if (!match.StatisticsLineupCaptured)
            {
                // Older active fixtures may predate kickoff statistics.
                appeared.UnionWith(match.HomeActiveStarterIds.Concat(match.AwayActiveStarterIds));
                appeared.UnionWith(match.HomeSubstitutedInIds.Concat(match.AwaySubstitutedInIds));
                appeared.UnionWith(match.HomeSubstitutedOutIds.Concat(match.AwaySubstitutedOutIds));
                appeared.UnionWith(match.HomeSentOffIds.Concat(match.AwaySentOffIds));
                appeared.UnionWith(match.HomeInjuredInMatchIds.Concat(match.AwayInjuredInMatchIds));
            }
            if (classicInjuries)
                injured.UnionWith(match.HomeInjuredInMatchIds.Concat(match.AwayInjuredInMatchIds));
        }

        w.PlayerStrengthHistoryStartSeason ??= w.SeasonNumber;
        w.PlayerStrengthHistoryStartRound ??= w.RoundNumber;
        foreach (var p in w.Players)
        {
            var club = p.ClubId is Guid id ? clubs.GetValueOrDefault(id) : null;
            var context = ClubContext(w, club);
            int before = p.Strength;
            bool played = appeared.Contains(p.Id);
            if (played)
            {
                // A new day-based injury already owns the strength loss for this episode.
                if (before > 1 && !injured.Contains(p.Id) && DeterministicRng.FromContext(
                    w.Seed, w.SeasonNumber, w.RoundNumber, PlayerToken(p.Id), 0, 12042).NextBool(PlayedDeclineProbability))
                    p.Strength--;
            }
            else if (before < context.Ceiling) p.Strength++;

            if (before != p.Strength)
            {
                string reason = played ? "PLAYED_MATCH_DECLINE" : "RESERVE_PROGRESS";
                Record(w, p, club, context.Position, context.Ceiling, before, reason,
                    $"{w.Id}:S{w.SeasonNumber}:R{w.RoundNumber}:P{p.Id}:{reason}");
            }
        }
        state.GuardSeason = w.SeasonNumber;
        state.GuardRound = w.RoundNumber;
        if (classicInjuries) CaptureTurns(w);
    }

    public static (int Position, int Ceiling) ClubContext(GameWorld w, Club? club)
    {
        int rank = club == null ? 4 : w.ClassicForce.CompletedLeaguePositions.GetValueOrDefault(club.Id, 4);
        return (rank, club == null ? 1 : GrowthCeiling(club.DivisionTier, rank));
    }

    private static void CaptureTurns(GameWorld w)
    {
        ILookup<Guid?, PlayerStrengthChangeDto>? changesByClub = null;
        foreach (var club in w.Clubs.Where(c => c.DivisionTier != DivisionTier.Distrital))
        {
            int half = club.LeaguePlayed == 7 ? 1 : club.LeaguePlayed == 14 ? 2 : 0;
            if (half == 0 || w.ClassicForce.TurnSnapshots.Any(s => s.Season == w.SeasonNumber && s.Half == half && s.ClubId == club.Id)) continue;
            var context = ClubContext(w, club);
            changesByClub ??= w.PlayerStrengthHistory.Where(e => e.Season == w.SeasonNumber && e.Rule == Rule).ToLookup(e => e.ClubId);
            var changes = changesByClub[club.Id];
            var byPlayer = changes.ToLookup(e => e.PlayerId);
            var roster = w.Players.Where(p => p.ClubId == club.Id).Select(p =>
            {
                var events = byPlayer[p.Id];
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
        p.MarketPrice = MarketAndEconomyEngine.CalculateFairValue(w, p);
        w.PlayerStrengthHistory.Add(new(key, p.Id, w.SeasonNumber, w.RoundNumber, club?.Id, club?.Name ?? "Sem clube",
            club?.DivisionTier, before, p.Strength, reason, injury?.Id, Rule, reason)
        {
            WorldId = w.Id, GameDate = date ?? (w.ForceEvolutionMode == ForceEvolutionMode.ClassicAdapted ? w.ClassicForce.GameDate : null),
            LeaguePosition = rank, GrowthCeiling = ceiling,
            InjuryDuration = injury?.Duration, DailyInjuryLoss = reason == "INJURY_DAILY_LOSS" ? before - p.Strength : 0,
            ReserveGrowth = reason == "RESERVE_PROGRESS" ? p.Strength - before : 0
        });
    }

    private static int PlayerToken(Guid id) => BinaryPrimitives.ReadInt32LittleEndian(id.ToByteArray()) & 0x3fffffff;
}
