namespace Elifoot.Contracts;

public record RenewalNegotiationCommandDto(
    Guid CoachId, decimal OfferedSalary, bool Accept, string TermsToken,
    string? IdempotencyKey = null, long? ExpectedRoundVersion = null);
