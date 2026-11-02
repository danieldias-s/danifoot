using System.Text.Json.Serialization;
namespace Elifoot.Contracts;

public record WorldClubProfile016B(int ClubInitialPower, int WorldRank, double? WorldRankingScore,
    string RankingSource, DateTime RankingDate, double RankingConfidence, int TargetInitialSquadSize = 0,
    bool EliteRuleApplied = false, string SquadSelectionMethod = "position-relative-v1", bool ManualPower = false, string? ManualPowerEvidence = null);
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InitialCareerChoice016B { PENDING, MANUAL, UNEMPLOYED, FOURTH_DIVISION_DRAW }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorldLobbyPhase016B { CONFIGURING, WAITING_FOR_PLAYERS, READY_FOR_DRAW, DRAW_COMPLETED, READY_TO_START, IN_GAME }
public record LobbyChoice016B(InitialCareerChoice016B Choice, Guid? ClubId = null);
public record LobbyParticipant016B(Guid CoachId, string Nickname, bool Connected, InitialCareerChoice016B Choice, Guid? ClubId, string? ClubName);
public record WorldLobbyView016B(Guid WorldId, string GameModeId, Guid HostCoachId, WorldLobbyPhase016B State,
    int Capacity, int Registered, int Connected, bool CanDraw, bool CanStart, bool DrawCompleted,
    long Version, List<LobbyParticipant016B> Participants, List<string> Issues);
public record LobbyCommand016B(string IdempotencyKey);
public record LobbyCapacity016B(int MaxHumans, string IdempotencyKey);
