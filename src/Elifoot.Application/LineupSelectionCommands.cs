using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Application;

public sealed partial class MultiplayerRoundOrchestrator
{
    public async Task<(bool Success, string? Error, LineupSelectionDto? Selection)> SelectLineupAsync(
        Guid worldId, SelectLineupRequest request, CancellationToken ct = default)
    {
        var gate = GetLock(worldId);
        await gate.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldAsync(worldId, ct);
            if (world == null) return (false, "Sala não encontrada.", null);
            var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == request.CoachId);
            var club = world.Clubs.FirstOrDefault(c => c.Id == coach?.CurrentClubId && c.HumanCoachId == coach!.Id);
            if (coach == null || club == null) return (false, "Você não controla este clube.", null);
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 160)
                return (false, "Identificador da seleção é obrigatório e deve ter até 160 caracteres.", null);
            if (!Enum.IsDefined(request.Mode) || !TacticalFormations.TryGet(request.Formation, world.AllowClassic505, out var formation))
                return (false, "Modo ou formação inválidos.", null);
            var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
            var current = request.CurrentStarterPlayerIds ?? club.PreferredStarterIds;
            if (current.Count > 11 || current.Distinct().Count() != current.Count || current.Any(id => squad.All(p => p.Id != id)))
                return (false, "Os titulares informados devem pertencer ao seu clube, sem duplicatas.", null);
            string key = request.CoachId + ":" + request.IdempotencyKey;
            string payload = JsonSerializer.Serialize(new { request.Formation, request.Mode, Current = request.CurrentStarterPlayerIds?.Order().ToArray() });
            if (world.LineupSelectionReceipts.TryGetValue(key, out var receipt))
            {
                if (receipt.Season != world.SeasonNumber || receipt.Round != world.RoundNumber)
                    return (false, "Seleção pertence a outra rodada.", null);
                if (receipt.Payload != payload) return (false, "Chave usada por outra seleção.", null);
                // A retry must also finish durability after a failed original save.
                await _repository.SaveWorldAsync(world, CancellationToken.None);
                return (true, null, receipt.Result);
            }
            if (world.RoundState != RoundState.PREPARING || coach.IsReadyForRound)
                return (false, "Selecione entre partidas e retire o pronto antes de mudar a escalação.", null);
            if (request.ExpectedRoundVersion != world.RoundVersion)
                return (false, "A sala mudou. Atualize antes de selecionar a escalação.", null);
            PlayerStatisticsEngine.EnsureMigrated(world);
            var selected = LineupSelectionEngine.Select(squad, formation, request.Mode, current, world);
            var valid = SquadValidator.ValidatePreMatchLineup(request.Formation, selected.Starters, selected.Bench, squad, world.AllowClassic505);
            if (!valid.IsValid) return (false, valid.ErrorMessage, null);
            club.LineupSelectionMode = request.Mode;
            club.PreferredFormation = request.Formation;
            club.PreferredStarterIds = selected.Starters.ToList();
            club.PreferredBenchIds = selected.Bench.ToList();
            world.RoundVersion++;
            var result = new LineupSelectionDto(world.Id, club.Id, request.Formation, request.Mode,
                selected.Starters, selected.Bench, world.RoundVersion);
            foreach (var old in world.LineupSelectionReceipts.Where(p => p.Value.Season != world.SeasonNumber || p.Value.Round != world.RoundNumber).Select(p => p.Key).ToList())
                world.LineupSelectionReceipts.Remove(old);
            world.LineupSelectionReceipts[key] = new(world.SeasonNumber, world.RoundNumber, payload, result);
            await _repository.SaveWorldAsync(world, CancellationToken.None);
            await _publisher.PublishReadyCounterUpdatedAsync(world.Id, BuildReadyCounterDto(world), ct);
            return (true, null, result);
        }
        finally { gate.Release(); }
    }
}
