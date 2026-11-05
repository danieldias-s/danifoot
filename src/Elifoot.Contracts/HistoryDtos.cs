namespace Elifoot.Contracts;

public record HistoryTotalsDto(int Games,int Wins,int Draws,int Losses,int GoalsFor,int GoalsAgainst)
{ public int GoalDifference => GoalsFor-GoalsAgainst; }
public record HistoryMatchDto(Guid MatchId,int Season,int Round,CompetitionType Competition,DivisionTier? Division,
    Guid HomeClubId,string HomeClub,int HomeGoals,Guid AwayClubId,string AwayClub,int AwayGoals,Guid? WinnerClubId);
public record ClubSeasonHistoryDto(int Season,DivisionTier? Division,int? FinalPosition,bool Closed,bool Partial,
    bool Promoted,bool Relegated,string? CupCampaign,HistoryTotalsDto Total,HistoryTotalsDto League,HistoryTotalsDto Cup,
    decimal? OpeningCash,decimal? ClosingCash,decimal Income,decimal Expenses,List<HistoryScorerDto> Scorers);
public record HistoryTitleDto(int Season,string Competition,DivisionTier? Division,Guid ClubId,string ClubName,bool RunnerUp=false);
public record HistoryScorerDto(Guid PlayerId,string PlayerName,int Goals);
public record HistoryCoachDto(Guid CoachId,string Name,bool Human,int StartSeason,int StartRound,int? EndSeason,int? EndRound,
    string? ExitReason,HistoryTotalsDto Results,int Titles,bool Partial=false);
public record HistoryTransferDto(Guid Id,Guid PlayerId,string PlayerName,Guid? FromClubId,string FromClub,
    Guid? ToClubId,string ToClub,int Season,int Round,decimal Amount,string Operation);
public record HistoryMembershipDto(Guid PlayerId,string PlayerName,Guid? ClubId,string ClubName,
    int StartSeason,int StartRound,int? EndSeason,int? EndRound,bool Partial);
public record PlayerSeasonStrengthDto(int Season,int? InitialStrength,int? FinalStrength,bool Partial);
public record HistoryRecognitionDto(Guid PlayerId,int Season,string Scope,int Goals);
public record HistoryRecordsDto(HistoryMatchDto? BiggestWin,HistoryMatchDto? BiggestLoss,int? LongestWinningRun,int? LongestUnbeatenRun);
public record HistoryPlayerSeasonTotalsDto(int Season,int Games,int Goals,int Assists,int Minutes,bool Partial,bool IncompleteAssists);
public record ClubHistoryDto(Guid ClubId,string ClubName,DivisionTier CurrentDivision,List<ClubSeasonHistoryDto> Seasons,
    HistoryTotalsDto Total,List<HistoryTitleDto> Titles,List<HistoryCoachDto> Coaches,List<HistoryMembershipDto> Players,
    List<HistoryTransferDto> Transfers,HistoryRecordsDto Records,List<string> CoverageWarnings);
