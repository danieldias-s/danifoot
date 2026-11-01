using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class CatalogMembershipPolicy
{
    public static CatalogMembershipStatus Classify(DateOnly? start, DateOnly? end, DateOnly asOf)
    {
        if (end < asOf || start > asOf) return CatalogMembershipStatus.Historical;
        if (start == null) return CatalogMembershipStatus.Unknown;
        return end == null ? CatalogMembershipStatus.OpenEndedUnconfirmed : CatalogMembershipStatus.DatedActive;
    }
    public static bool Usable(CatalogAthlete athlete, DateOnly asOf) =>
        (athlete.MembershipStart == null || athlete.MembershipStart <= asOf) &&
        (athlete.MembershipEnd == null || athlete.MembershipEnd >= asOf) &&
        (athlete.MembershipEvidence?.Status == CatalogMembershipStatus.ProviderReported
            ? athlete.PlayerOrigin == WorldPlayerOrigin.REAL_CURRENT_PROVIDER_REPORTED && athlete.MembershipEvidence.AsOf <= asOf && athlete.MembershipEvidence.AsOf >= asOf.AddDays(-7)
            : athlete.MembershipEvidence == null || athlete.MembershipEvidence.Status == CatalogMembershipStatus.DatedActive &&
            Classify(athlete.MembershipStart, athlete.MembershipEnd, asOf) == CatalogMembershipStatus.DatedActive);
}
