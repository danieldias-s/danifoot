namespace Elifoot.Contracts;

public record TransferContractPreviewDto(Guid PlayerId,string DisplayName,Guid BuyerClubId,string BuyerClubName,
    Guid? SellerClubId,string? SellerClubName,decimal? AskingPrice,decimal CurrentSalaryPerRound,
    decimal SigningSalaryPerRound,decimal AdditionalCosts,string SalaryTermsToken,long ListingVersion,
    int ContractRounds,bool IsEstimate,long RoundVersion)
{
    public decimal CalculatedBaseSalary { get; init; }
    public decimal MarketValue { get; init; }
    public decimal CandidateSigningPrice { get; init; }
    public string NationalityClass { get; init; } = "SAME_COUNTRY";
    public decimal AiHiringInterestFactor { get; init; } = 1.50m;
    public double InflationIndex { get; init; } = 2.0;
    public int InflationDisplay { get; init; } = 20;
}
