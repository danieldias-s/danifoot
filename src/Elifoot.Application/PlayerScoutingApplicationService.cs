using System.Globalization;
using System.Text;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Application;

public sealed partial class ClubAndMarketApplicationService
{
    private static string SearchName(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();

    public async Task<PlayerScoutingDto?> GetPlayerScoutingAsync(Guid worldId, Guid coachId,
        string? name = null, bool observedOnly = false, CancellationToken ct = default)
    {
        var gate = MultiplayerRoundOrchestrator.GetLock(worldId);
        await gate.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldAsync(worldId, ct);
            var coach = world?.HumanCoaches.FirstOrDefault(c => c.Id == coachId);
            if (world == null || coach == null) return null;
            var observed = coach.ObservedPlayerIds.ToHashSet();
            var query = SearchName((name ?? "").Trim());
            var players = world.Players.Where(p => (!observedOnly || observed.Contains(p.Id)) &&
                (query.Length >= 3 ? SearchName(p.DisplayName).Contains(query) : observedOnly && query.Length == 0));
            var clubs = world.Clubs.ToDictionary(c => c.Id, c => c.Name);
            var auctions = world.Auctions.Where(a => !a.IsClosed).Select(a => a.PlayerId).ToHashSet();
            return new(observed.OrderBy(id => id).ToList(), players.OrderBy(p => p.DisplayName).ThenBy(p => p.Id)
                .Select(p => new ScoutingPlayerDto(p.Id, p.DisplayName, p.Position, p.Strength, p.ClubId,
                    p.ClubId is Guid clubId ? clubs.GetValueOrDefault(clubId, "Clube indisponível") : "Livre",
                    p.IsListedForTransfer, auctions.Contains(p.Id))).ToList());
        }
        finally { gate.Release(); }
    }

    public async Task<(bool Success, string? Error, List<Guid>? ObservedPlayerIds)> SetPlayerObservationAsync(
        Guid worldId, Guid coachId, Guid playerId, bool observed, CancellationToken ct = default)
    {
        var gate = MultiplayerRoundOrchestrator.GetLock(worldId);
        await gate.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldAsync(worldId, ct);
            var coach = world?.HumanCoaches.FirstOrDefault(c => c.Id == coachId);
            if (world == null || coach == null) return (false, "Treinador não pertence à sala.", null);
            if (!world.Players.Any(p => p.Id == playerId)) return (false, "Jogador não encontrado neste mundo.", null);
            bool changed = observed ? !coach.ObservedPlayerIds.Contains(playerId) : coach.ObservedPlayerIds.Contains(playerId);
            if (changed)
            {
                if (observed) coach.ObservedPlayerIds.Add(playerId);
                else coach.ObservedPlayerIds.RemoveAll(id => id == playerId);
                // Personal scouting is durable, including when saved between clock checkpoints.
                world.RequiresDurableMatchInteractionSave = true;
                await _repository.SaveWorldAsync(world, ct);
            }
            return (true, null, coach.ObservedPlayerIds.Distinct().OrderBy(id => id).ToList());
        }
        finally { gate.Release(); }
    }
}
