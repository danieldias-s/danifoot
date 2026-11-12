using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Application;

public sealed partial class ClubAndMarketApplicationService
{
    public Task<(bool Success, string? Error, Guid? AuctionId)> DecidePlayerRenewalOfferAsync(
        Guid worldId, Guid playerId, RenewalNegotiationCommandDto command, CancellationToken ct = default)
        => Finance<(bool, string?, Guid?)>(worldId, command.CoachId, "RENEWAL_NEGOTIATION",
            FormattableString.Invariant($"{playerId}:{command.OfferedSalary}:{command.Accept}:{command.TermsToken}"),
            command.IdempotencyKey, command.ExpectedRoundVersion, e => (false, e, null), async key =>
            {
                var world = (await _repository.GetWorldAsync(worldId, ct))!;
                var club = world.Clubs.First(c => c.HumanCoachId == command.CoachId);
                var player = world.Players.FirstOrDefault(p => p.Id == playerId);
                return player == null ? (false, "Jogador não encontrado.", null)
                    : ClassicEconomyEngine.DecideRenewalOffer(world, club, player, command.OfferedSalary,
                        command.Accept, command.TermsToken, key);
            }, ct);
}
