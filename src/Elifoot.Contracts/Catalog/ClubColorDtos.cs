namespace Elifoot.Contracts;

/// <summary>Traditional palette facts; RGB representation precision is recorded separately from identity verification.</summary>
public record ClubColorPalette(string PrimaryColor, string? SecondaryColor, string? PreferredTextColor,
    string ColorSource, bool ColorVerified, DateTime LastColorUpdate, double ColorConfidence, string Version,
    string CountryCode, FootballGender Gender, string Provider, string ExternalId, string Evidence,
    bool IsPersonalized = false, string Precision = "TraditionalRepresentative");
public record AppearancePreferencesDto(string Mode = "Club", string? Background = null, string? Primary = null,
    string? Secondary = null, string? Text = null, string? Accent = null, int Version = 0);
public record SaveAppearanceRequest(AppearancePreferencesDto Preferences, int ExpectedVersion);
public record AppearanceStateDto(Guid WorldId, Guid ClubId, ClubColorPalette? ClubColors, AppearancePreferencesDto Preferences);
public record ImportClubColorRequest(string CountryCode, FootballGender Gender, string Provider, string ExternalId,
    ClubColorPalette Palette);
public record ClubColorCoverageDto(Guid ClubId, string CountryCode, FootballGender Gender, string Name,
    ClubColorPalette? Palette, string Coverage, DateTime? LastAttempt, string? LastIssue);
