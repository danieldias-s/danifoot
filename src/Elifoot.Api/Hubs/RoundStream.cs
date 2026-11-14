using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Elifoot.Contracts;

namespace Elifoot.Api.Hubs;

/// <summary>Only the latest published projection of a subscribed room is retained.</summary>
public sealed class RoundStream
{
    private sealed record RoomProjection(long Sequence, LiveRoundSnapshotDto? Snapshot);
    private readonly ConcurrentDictionary<Guid, RoomProjection> _latest = new();
    private readonly Guid _epoch = Guid.NewGuid();
    private long _sequence;

    // Callers hold the room lock across reads, subscription changes and publication.
    public LiveRoundSnapshotDto Stamp(LiveRoundSnapshotDto snapshot) => snapshot with
    {
        RealtimeEpoch = _epoch,
        RealtimeSequence = _latest.GetValueOrDefault(snapshot.GameWorldId)?.Sequence ?? Interlocked.Read(ref _sequence)
    };

    public bool IsCurrent(Guid worldId, Guid? epoch, long? sequence) =>
        epoch == _epoch && (_latest.TryGetValue(worldId, out var latest)
            ? sequence == latest.Sequence
            : sequence >= 0 && sequence <= Interlocked.Read(ref _sequence));

    public void Forget(Guid worldId) => _latest.TryRemove(worldId, out _);

    public (string Event, object Payload) Publish(LiveRoundSnapshotDto snapshot, bool allowPatch)
    {
        var previous = _latest.GetValueOrDefault(snapshot.GameWorldId)?.Snapshot;
        var next = snapshot with { RealtimeEpoch = _epoch, RealtimeSequence = Interlocked.Increment(ref _sequence) };
        bool live = next.RoundState is not (RoundState.RESULTS or RoundState.FINISHED or RoundState.PREPARING);
        _latest[next.GameWorldId] = new(next.RealtimeSequence.Value, live ? next : null);
        if (!allowPatch || previous == null || next.RoundVersion != previous.RoundVersion + 1 ||
            next.SeasonNumber != previous.SeasonNumber || next.RoundNumber != previous.RoundNumber ||
            next.Matches.Count != previous.Matches.Count || next.GameDate != previous.GameDate ||
            next.CompetitionType != previous.CompetitionType || next.GameModeId != previous.GameModeId ||
            next.WorldName != previous.WorldName || next.CompetitionLabel != previous.CompetitionLabel ||
            next.ForceEvolutionMode != previous.ForceEvolutionMode || next.TotalRoundsInSeason != previous.TotalRoundsInSeason ||
            next.ForceRulesVersion != previous.ForceRulesVersion || next.DisciplineRulesVersion != previous.DisciplineRulesVersion ||
            next.HasInitialLobby != previous.HasInitialLobby || next.LobbyStarted != previous.LobbyStarted)
            return ("RoundSyncV2", next);

        var matches = new List<MatchChange>();
        for (int index = 0; index < next.Matches.Count; index++)
        {
            var before = previous.Matches[index];
            var after = next.Matches[index];
            if (before.MatchId != after.MatchId || !StableMatch(before).Equals(StableMatch(after))
                || before.Events.Count > after.Events.Count)
                return ("RoundSyncV2", next);
            // During a clock tick the match engine only appends events. Commands which
            // can replace any part of the projection publish a complete sync point.

            var score = Score(after);
            var expectedMinute = !before.IsFinished && before.Minute == previous.CurrentMinute
                ? next.CurrentMinute : before.Minute;
            bool scoreChanged = score != (Score(before) with { Minute = expectedMinute });
            var homeStarters = ChangedLineup(before.HomeStarters, after.HomeStarters);
            var homeBench = ChangedLineup(before.HomeBench, after.HomeBench);
            var awayStarters = ChangedLineup(before.AwayStarters, after.AwayStarters);
            var awayBench = ChangedLineup(before.AwayBench, after.AwayBench);
            var events = before.Events.Count == after.Events.Count ? null : after.Events.GetRange(before.Events.Count, after.Events.Count - before.Events.Count);
            if (scoreChanged || events != null || homeStarters != null || homeBench != null || awayStarters != null || awayBench != null)
                matches.Add(new(index, scoreChanged ? score : null, events, homeStarters, homeBench, awayStarters, awayBench));
        }

        var counter = next.ReadyCounter;
        var oldCounter = previous.ReadyCounter;
        bool counterChanged = (counter with { RoundVersion = oldCounter.RoundVersion, Participants = oldCounter.Participants }) != oldCounter
            || !counter.Participants.SequenceEqual(oldCounter.Participants);
        return ("RoundPatchV2", new RoundPatch(next.GameWorldId, _epoch, previous.RealtimeSequence!.Value,
            next.RealtimeSequence!.Value, previous.RoundVersion, next.RoundVersion, next.PersistedRoundVersion,
            next.CurrentMinute, next.RoundState, next.ActivePauseWindow, next.PendingPenalty,
            counterChanged ? counter : null, matches));
    }

    private static List<MatchLineupPlayerDto>? ChangedLineup(List<MatchLineupPlayerDto> before, List<MatchLineupPlayerDto> after)
        => before.SequenceEqual(after) ? null : after;

    // Minute follows the room clock for ordinary live matches.
    private static MatchScore Score(LiveMatchSummaryDto m) => new(m.HomeGoals, m.AwayGoals, m.HomePenaltyGoals,
        m.AwayPenaltyGoals, m.IsFinished, m.IsExtraTime, m.Minute,
        m.HomeFormation, m.AwayFormation, m.HomeSubCount, m.HomeMaxSubs, m.AwaySubCount, m.AwayMaxSubs,
        m.HomePauseWindowsUsed, m.HomeMaxPauseWindows, m.AwayPauseWindowsUsed, m.AwayMaxPauseWindows,
        m.HomePlayersOnPitch, m.AwayPlayersOnPitch);

    private static LiveMatchSummaryDto StableMatch(LiveMatchSummaryDto m) => m with
    {
        Minute = 0, HomeGoals = 0, AwayGoals = 0, HomePenaltyGoals = null, AwayPenaltyGoals = null,
        IsFinished = false, IsExtraTime = false, HomeFormation = "", AwayFormation = "",
        HomeSubCount = 0, HomeMaxSubs = 0, AwaySubCount = 0, AwayMaxSubs = 0,
        HomePauseWindowsUsed = 0, HomeMaxPauseWindows = 0, AwayPauseWindowsUsed = 0, AwayMaxPauseWindows = 0,
        HomePlayersOnPitch = 0, AwayPlayersOnPitch = 0,
        Events = EmptyEvents, HomeStarters = EmptyLineup, HomeBench = EmptyLineup, AwayStarters = EmptyLineup, AwayBench = EmptyLineup
    };

    private static readonly List<MatchEventDto> EmptyEvents = [];
    private static readonly List<MatchLineupPlayerDto> EmptyLineup = [];

    public sealed record RoundPatch(Guid GameWorldId, Guid RealtimeEpoch, long PreviousSequence, long RealtimeSequence,
        long PreviousVersion, long RoundVersion, long? PersistedRoundVersion, int CurrentMinute, RoundState RoundState,
        PauseWindowStateDto? ActivePauseWindow, PendingPenaltyDecisionDto? PendingPenalty,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReadyCounterDto? ReadyCounter,
        List<MatchChange> Matches);

    public sealed record MatchChange(int Index,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MatchScore? Score,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<MatchEventDto>? Events,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<MatchLineupPlayerDto>? HomeStarters,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<MatchLineupPlayerDto>? HomeBench,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<MatchLineupPlayerDto>? AwayStarters,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<MatchLineupPlayerDto>? AwayBench);

    public sealed record MatchScore(int HomeGoals, int AwayGoals, int? HomePenaltyGoals, int? AwayPenaltyGoals,
        bool IsFinished, bool IsExtraTime, int Minute, string HomeFormation, string AwayFormation,
        int HomeSubCount, int HomeMaxSubs, int AwaySubCount, int AwayMaxSubs,
        int HomePauseWindowsUsed, int HomeMaxPauseWindows, int AwayPauseWindowsUsed, int AwayMaxPauseWindows,
        int HomePlayersOnPitch, int AwayPlayersOnPitch);
}
