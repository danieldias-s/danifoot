namespace Elifoot.Contracts;

public record PlayerObservationRequest(Guid CoachId, bool IsObserved);
public record ScoutingPlayerDto(Guid PlayerId, string DisplayName, PlayerPosition Position,
    int Strength, Guid? ClubId, string ClubName, bool IsListedForTransfer, bool IsInAuction);
public record PlayerScoutingDto(List<Guid> ObservedPlayerIds, List<ScoutingPlayerDto> Players);
