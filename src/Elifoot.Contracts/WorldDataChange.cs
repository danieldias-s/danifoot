namespace Elifoot.Contracts;

public sealed record WorldDataChange(Guid WorldId, long Version, int SeasonNumber, int RoundNumber,
    RoundState RoundState, string[] Areas);
