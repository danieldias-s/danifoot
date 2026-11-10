using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Application;

public sealed partial class MultiplayerRoundOrchestrator
{
    private static string? InteractionKey(string kind, Guid coach, string key) => string.IsNullOrWhiteSpace(key) ? null : $"{kind}:{coach}:{key}";
    private static string InteractionFingerprint<T>(T command) => JsonSerializer.Serialize(command);
    private static void RecordInteraction(GameWorld world, string? key, string fingerprint, Guid? pause = null)
    {
        if (key != null) world.MatchInteractionReceipts[key] = new MatchInteractionReceipt { Fingerprint = fingerprint, PauseWindowId = pause };
        world.RequiresDurableMatchInteractionSave = true;
    }
    private static bool RequiresHumanPenaltyChoice(GameWorld world, Club club) => club.HumanCoachId != null && world.HumanCoaches.Any(h => h.Id == club.HumanCoachId && h.CurrentClubId == club.Id && !h.IsAutoMode);
    private static (MatchFixture Match, PendingPenaltyKick Kick)? FirstPendingPenalty(GameWorld world)
    {
        foreach (var match in GetCurrentRoundFixtures(world).OrderBy(m => m.DeterministicMatchIndex))
            if (match.PendingPenalties.FirstOrDefault() is { } kick) return (match, kick);
        return null;
    }
    private static PendingPenaltyDecisionDto? BuildPendingPenaltyDto(GameWorld world)
    {
        if (FirstPendingPenalty(world) is not { } pending) return null;
        var kick = pending.Kick;
        var players = world.Players.ToDictionary(p => p.Id);
        return new(kick.Id, pending.Match.Id, kick.CoachId,
            world.HumanCoaches.FirstOrDefault(h => h.Id == kick.CoachId)?.Nickname ?? "Treinador",
            kick.ClubId, world.Clubs.First(c => c.Id == kick.ClubId).Name, kick.Minute,
            kick.EligiblePlayerIds.Where(players.ContainsKey).Select(id => players[id]).Select(p => new PenaltyTakerChoiceDto(p.Id,p.DisplayName,p.Position,p.Strength,p.IsStar)).ToList());
    }
    public async Task<(bool Success, string? Error, LiveRoundSnapshotDto? Snapshot)> ChoosePenaltyTakerAsync(ChoosePenaltyTakerCommand command, CancellationToken ct = default)
    {
        var sem = GetLock(command.GameWorldId); await sem.WaitAsync(ct);
        try {
            var world = await _repository.GetWorldAsync(command.GameWorldId, ct);
            if (world == null) return (false,"Sala não encontrada.",null);
            var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == command.CoachId);
            if (coach == null) return (false,"Treinador não pertence à sala.",BuildLiveRoundSnapshotDto(world));
            if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) return (false,"Informe a chave da operação.",BuildLiveRoundSnapshotDto(world));
            var key = InteractionKey("penalty",command.CoachId,command.IdempotencyKey)!;
            var fingerprint = JsonSerializer.Serialize(new { command.GameWorldId, command.CoachId, command.DecisionId, command.PlayerId });
            var decisionKey = $"penalty-decision:{command.DecisionId}";
            foreach (var receiptKey in new[] { key, decisionKey })
                if (world.MatchInteractionReceipts.TryGetValue(receiptKey,out var receipt))
                    return receipt.Fingerprint == fingerprint ? (true,null,BuildLiveRoundSnapshotDto(world)) : (false,"Cobrança já decidida ou chave reutilizada com dados diferentes.",BuildLiveRoundSnapshotDto(world));
            if (FirstPendingPenalty(world) is not { } pending || pending.Kick.Id != command.DecisionId)
                return (false,"Esta cobrança não está mais aguardando escolha.",BuildLiveRoundSnapshotDto(world));
            var kick=pending.Kick; var match=pending.Match;
            if (kick.CoachId != coach.Id || coach.CurrentClubId != kick.ClubId)
                return (false,"Somente o treinador do clube cobrador pode escolher o jogador.",BuildLiveRoundSnapshotDto(world));
            var active = match.HomeClubId == kick.ClubId ? match.HomeActiveStarterIds : match.AwayActiveStarterIds;
            var injured = match.HomeClubId == kick.ClubId ? match.HomeInjuredInMatchIds : match.AwayInjuredInMatchIds;
            var sentOff = match.HomeClubId == kick.ClubId ? match.HomeSentOffIds : match.AwaySentOffIds;
            var taker=world.Players.FirstOrDefault(p => p.Id==command.PlayerId && p.ClubId==kick.ClubId);
            if(taker==null || !kick.EligiblePlayerIds.Contains(taker.Id) || !active.Contains(taker.Id) || injured.Contains(taker.Id) || sentOff.Contains(taker.Id) || (match.ForceRulesVersion==ClassicForceEvolutionEngine.Version && taker.InjuryDaysRemaining>0))
                return (false,"Escolha um jogador apto que esteja em campo.",BuildLiveRoundSnapshotDto(world));
            var club=world.Clubs.First(c=>c.Id==kick.ClubId);
            MatchSimulationEngine.ResolvePenaltyKick(match,club,taker,world.Players.FirstOrDefault(p=>p.Id==kick.GoalkeeperId),kick.GoalkeeperPower,match.HomeClubId==club.Id,kick.Minute,kick.ConversionRoll,new List<MatchEventRecord>());
            match.PendingPenalties.Remove(kick);
            RecordInteraction(world,key,fingerprint); RecordInteraction(world,decisionKey,fingerprint);
            if(match.PendingPenalties.Count==0) {
                var players=world.Players.ToDictionary(p=>p.Id);
                foreach(bool home in new[]{true,false}) {
                    var side=world.Clubs.First(c=>c.Id==(home?match.HomeClubId:match.AwayClubId));
                    if(side.HumanCoachId==null) AiManagerBrain.EvaluateAiInMatchReaction(match,side,world.AiManagers.FirstOrDefault(m=>m.Id==side.AiManagerId),home,world.RoundState,players,world.AllowClassic505,world);
                }
            }
            if(FirstPendingPenalty(world)==null) ApplyCurrentMinuteBoundary(world);
            world.RoundVersion++; await _repository.SaveWorldAsync(world,ct);
            var snapshot=BuildLiveRoundSnapshotDto(world);await _publisher.PublishRoundStateChangedAsync(world.Id,snapshot,ct);
            return (true,null,snapshot);
        } finally { sem.Release(); }
    }
    private static void ApplyCurrentMinuteBoundary(GameWorld world)
    {
        int minute=world.CurrentMatchMinute;
        if(minute!=45 && minute!=90 && minute!=105 && minute<120) return;
        var roundMatches=GetCurrentRoundFixtures(world);
        var clubDict=world.Clubs.ToDictionary(c=>c.Id);
        var aiDict=world.AiManagers.ToDictionary(m=>m.Id);
        var playerMap=world.Players.ToDictionary(p=>p.Id);
        foreach(var match in roundMatches.Where(m=>m.StatisticsLineupCaptured)) ClassicForceEvolutionEngine.CaptureRoster(match,playerMap);
            // Check phase boundary transitions at 45', 90', 105', 120'
            if (minute == 45 && world.RoundState == RoundState.RUNNING_FIRST_HALF)
            {
                // Let AI teams evaluate halftime adjustments during the free interval
                world.RoundState = RoundState.HALFTIME_PAUSED;
                EvaluateAllAiTeamsAtFreeInterval(world, roundMatches, clubDict, aiDict, playerMap);
                OpenFreeIntervalPauseWindow(world, RoundState.HALFTIME_PAUSED, 45);
            }
            else if (minute == 90 && world.RoundState == RoundState.RUNNING_SECOND_HALF)
            {
                bool anyMatchEntersExtraTime = false;
                foreach (var match in roundMatches.Where(m => !m.IsFinished))
                {
                    if (match.RequiresKnockoutResolution && match.HomeGoals == match.AwayGoals)
                    {
                        match.EnteredExtraTime = true; // Grants +1 sub (6) and +1 pause window (4)!
                        anyMatchEntersExtraTime = true;
                        match.Events.Add(new MatchEventRecord
                        {
                            Minute = 90,
                            EventType = MatchEventType.ExtraTimeStarted,
                            ClubId = match.HomeClubId,
                            ClubName = clubDict[match.HomeClubId].Name,
                            Description = "90' Empate no tempo normal! A partida vai para a PRORROGAÇÃO (+1 substituição e +1 parada)."
                        });
                    }
                    else
                    {
                        match.IsFinished = true;
                        match.WinnerClubId = match.HomeGoals > match.AwayGoals
                            ? match.HomeClubId
                            : (match.AwayGoals > match.HomeGoals ? match.AwayClubId : null);
                    }
                }

                if (anyMatchEntersExtraTime)
                {
                    // Check if any human is participating in the extra time matches, or open free interval break before extra time
                    bool humanInExtraTime = roundMatches
                        .Where(m => !m.IsFinished && m.EnteredExtraTime)
                        .Any(m => clubDict[m.HomeClubId].HumanCoachId != null || clubDict[m.AwayClubId].HumanCoachId != null);

                    EvaluateAllAiTeamsAtFreeInterval(world, roundMatches.Where(m => !m.IsFinished).ToList(), clubDict, aiDict, playerMap);

                    if (humanInExtraTime)
                    {
                        OpenFreeIntervalPauseWindow(world, RoundState.EXTRA_TIME_BREAK, 90);
                    }
                    else
                    {
                        world.RoundState = RoundState.RUNNING_EXTRA_TIME;
                    }
                }
                else
                {
                    CompleteAllMatchesAndTransitionToResults(world, roundMatches);
                }
            }
            else if (minute == 105 && world.RoundState == RoundState.RUNNING_EXTRA_TIME)
            {
                bool humanInExtraTime = roundMatches
                    .Where(m => !m.IsFinished && m.EnteredExtraTime)
                    .Any(m => clubDict[m.HomeClubId].HumanCoachId != null || clubDict[m.AwayClubId].HumanCoachId != null);

                EvaluateAllAiTeamsAtFreeInterval(world, roundMatches.Where(m => !m.IsFinished).ToList(), clubDict, aiDict, playerMap);

                if (humanInExtraTime)
                {
                    OpenFreeIntervalPauseWindow(world, RoundState.EXTRA_TIME_HALFTIME, 105);
                }
            }
            else if (minute >= 120 && world.RoundState == RoundState.RUNNING_EXTRA_TIME)
            {
                foreach (var match in roundMatches.Where(m => !m.IsFinished))
                {
                    if (match.HomeGoals == match.AwayGoals && match.RequiresKnockoutResolution)
                    {
                        MatchSimulationEngine.ResolvePenaltyShootout(
                            match, clubDict[match.HomeClubId], clubDict[match.AwayClubId], playerMap, world.Seed);
                    }
                    else
                    {
                        match.WinnerClubId = match.HomeGoals > match.AwayGoals ? match.HomeClubId : match.AwayClubId;
                    }
                    match.IsFinished = true;
                }

                CompleteAllMatchesAndTransitionToResults(world, roundMatches);
            }

    }
}
