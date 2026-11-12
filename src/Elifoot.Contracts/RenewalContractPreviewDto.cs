namespace Elifoot.Contracts;

public record RenewalContractPreviewDto(Guid PlayerId,Guid ClubId,int Season,int Round,
    decimal CurrentSalary,decimal RequiredSalary,decimal SigningFee,bool RequiresRaise,
    string TermsToken,long RoundVersion,decimal? OfferedSalary=null,
    decimal? OfferedSigningFee=null,bool? OfferAccepted=null);
