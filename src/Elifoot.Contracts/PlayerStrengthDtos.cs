namespace Elifoot.Contracts;

public record PlayerStrengthChangeDto(string IdempotencyKey, Guid PlayerId, int Season, int Round,
    Guid? ClubId, string ClubName, DivisionTier? Division, int StrengthBefore, int StrengthAfter,
    string ChangeReason, Guid? InjuryId, string Rule, string Detail)
{
    public Guid? WorldId { get; init; }
    public DateOnly? GameDate { get; init; }
    public int? LeaguePosition { get; init; }
    public int? GrowthCeiling { get; init; }
    public int? InjuryDuration { get; init; }
    public int DailyInjuryLoss { get; init; }
    public int ReserveGrowth { get; init; }
    public int ForceBefore => StrengthBefore;
    public int ForceAfter => StrengthAfter;
}
