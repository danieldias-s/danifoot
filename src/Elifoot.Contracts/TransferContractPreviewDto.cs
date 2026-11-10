namespace Elifoot.Contracts;

public record TransferContractPreviewDto(Guid PlayerId,string DisplayName,Guid BuyerClubId,string BuyerClubName,
    Guid? SellerClubId,string? SellerClubName,decimal? AskingPrice,decimal CurrentSalaryPerRound,
    decimal SigningSalaryPerRound,decimal AdditionalCosts,string SalaryTermsToken,long ListingVersion,
    int ContractRounds,bool IsEstimate,long RoundVersion);
