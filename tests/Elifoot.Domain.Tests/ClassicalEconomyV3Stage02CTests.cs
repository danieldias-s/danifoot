using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class ClassicalEconomyV3Stage02CTests
{
    private static readonly (int Strength, decimal CalibratedSalaryM, decimal ClassicalSalaryC1, decimal ClassicalSalaryC5, decimal ClassicalMarketValueNeutralC1)[] ExpectedStrengthTable =
    [
        (1, 222m, 100m, 500m, 4000m),
        (2, 227m, 200m, 1000m, 8225m),
        (3, 236m, 300m, 1500m, 12539m),
        (4, 249m, 400m, 2000m, 16912m),
        (5, 267m, 500m, 2500m, 21330m),
        (10, 418m, 1000m, 5000m, 43859m),
        (15, 681m, 1550m, 7750m, 66864m),
        (20, 1059m, 2100m, 10500m, 90184m),
        (25, 1554m, 2600m, 13000m, 113741m),
        (30, 2169m, 3150m, 15750m, 137488m),
        (35, 2906m, 3700m, 18500m, 161395m),
        (40, 3766m, 4300m, 21500m, 185440m),
        (45, 4751m, 4850m, 24250m, 209605m),
        (50, 5861m, 5400m, 27000m, 233878m)
    ];

    [Fact]
    public void TenMandatoryEconomicValues_AreExplicitlyDifferentiated()
    {
        var world = WorldSeedFactory.CreateSeededWorld(totalClubs: 32);
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(world, currencyFactorC: 1, recalculateInitialPlayerValues: true);

        var seller = world.Clubs[0];
        var buyer = world.Clubs[1];
        seller.Country = "Brasil";
        buyer.Country = "Brasil";

        var player = world.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
        player.Strength = 40;
        player.Behavior = PlayerBehavior.Cavalheiro; // A = 2 (-8%)
        player.PersonalModifierB = 3;                 // B = 3 (-6%)
        player.PersonalModifierD = 8;                 // D = 8 (+16%)
        player.IsStar = true;
        player.Nationality = "Brasil";
        player.SalaryPerRound = 3_100m;               // Negotiated current wage != base salary

        decimal negotiatedTransferPrice = 310_000m;
        var breakdown = ClassicPlayerEconomyEngine.EvaluatePlayerEconomy(
            world, player, buyer, seller, negotiatedPrice: negotiatedTransferPrice);

        // 1. Strength
        Assert.Equal(40, breakdown.Strength);
        // 2. Current salary (contracted)
        Assert.Equal(3_100m, breakdown.CurrentSalaryPerRound);
        // 3. Calculated base salary S (SEG03:7FF4)
        Assert.Equal(4_300m, breakdown.CalculatedBaseSalary);
        Assert.NotEqual(breakdown.CurrentSalaryPerRound, breakdown.CalculatedBaseSalary);
        // 4. Individual market value V (SEG09:371A)
        // Factor = 1 - 0.08 - 0.06 + 0.16 = 1.02 => Round(2000 * 1 * 2.0 * 40^1.04 * 1.02) = 189,148
        Assert.Equal(189_148m, breakdown.IndividualMarketValue);
        Assert.NotEqual(breakdown.CalculatedBaseSalary, breakdown.IndividualMarketValue);
        // 5. Candidate signing price Q (SEG03:63FA: (V + 30000) * 1.60 for SameCountry)
        Assert.Equal(Math.Round((189_148m + 30_000m) * 1.60m, 0, MidpointRounding.ToEven), breakdown.CandidateSigningPrice);
        Assert.NotEqual(breakdown.IndividualMarketValue, breakdown.CandidateSigningPrice);
        // 6. Final negotiated price
        Assert.Equal(310_000m, breakdown.FinalNegotiatedPrice);
        Assert.NotEqual(breakdown.CandidateSigningPrice, breakdown.FinalNegotiatedPrice);
        // 7. Minimum auction bid (voluntary default 500,000 vs mandatory 0)
        Assert.Equal(500_000m, breakdown.MinimumAuctionBid);
        // 8. Required renewal salary (Stage 02B rule)
        Assert.Equal(3_410m, breakdown.RequiredRenewalSalary);
        // 9. AI hiring interest factor (SEG03:5BB1: (1.50 + 0.05*0) * 1.10 = 1.65)
        Assert.Equal(1.65m, breakdown.AiHiringInterestFactor);
        // 10. Internal inflation index I (2.0 -> display 20)
        Assert.Equal(2.0, breakdown.InflationIndex);
        Assert.Equal(20, breakdown.InflationDisplayIndex);
    }

    [Fact]
    public void SalaryFormula_Strengths1To50_MatchesExecutableAndVerifiesRoundingClampsAndFloor()
    {
        for (int f = 1; f <= 50; f++)
        {
            decimal s1 = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(f, inflationIndex: 2.0, clubFactorR: 1.0, currencyFactorC: 1);
            decimal s5 = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(f, inflationIndex: 2.0, clubFactorR: 1.0, currencyFactorC: 5);

            Assert.Equal(0m, s1 % 50m);
            Assert.Equal(0m, s5 % 250m);
            Assert.Equal(s1 * 5m, s5);
            Assert.InRange(s1, 50m, 50_000m);
            Assert.InRange(s5, 250m, 250_000m);

            if (f > 1)
            {
                decimal prev = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(f - 1, inflationIndex: 2.0, clubFactorR: 1.0, currencyFactorC: 1);
                Assert.True(s1 >= prev);
            }
        }

        foreach (var row in ExpectedStrengthTable)
        {
            Assert.Equal(row.CalibratedSalaryM, MarketAndEconomyEngine.CalculateFairSalaryPerRound(row.Strength, false, PlayerPosition.M));
            Assert.Equal(row.ClassicalSalaryC1, ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(row.Strength, 2.0, 1.0, 1));
            Assert.Equal(row.ClassicalSalaryC5, ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(row.Strength, 2.0, 1.0, 5));
        }

        // Minimum clamp (50 * C) and maximum clamp (50000 * C)
        decimal minC1 = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(1, inflationIndex: 0.5, clubFactorR: 0.25, currencyFactorC: 1);
        decimal minC5 = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(1, inflationIndex: 0.5, clubFactorR: 0.25, currencyFactorC: 5);
        Assert.Equal(50m, minC1);
        Assert.Equal(250m, minC5);

        decimal maxC1 = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(50, inflationIndex: 10.0, clubFactorR: 25.0, currencyFactorC: 1);
        decimal maxC5 = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(50, inflationIndex: 10.0, clubFactorR: 25.0, currencyFactorC: 5);
        Assert.Equal(50_000m, maxC1);
        Assert.Equal(250_000m, maxC5);

        // Floor based on previous salary (SEG03:812C..814A)
        decimal withoutFloor = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(20, 2.0, 1.0, 1);
        decimal withHigherFloor = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(20, 2.0, 1.0, 1, currentSalaryFloor: 3_900m);
        decimal withLowerFloor = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(20, 2.0, 1.0, 1, currentSalaryFloor: 1_200m);
        Assert.Equal(2_100m, withoutFloor);
        Assert.Equal(3_900m, withHigherFloor);
        Assert.Equal(2_100m, withLowerFloor);
    }

    [Fact]
    public void SalaryLifecycle_NewPlayersExistingPlayersSigningRenewalAndRaise_FollowApprovedRules()
    {
        var world = WorldSeedFactory.CreateSeededWorld(totalClubs: 32);
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(world, currencyFactorC: 1, recalculateInitialPlayerValues: true);

        var seller = world.Clubs[0];
        var buyer = world.Clubs[1];
        seller.HumanCoachId = Guid.NewGuid();
        buyer.HumanCoachId = Guid.NewGuid();

        // 1. Newly initialized classical player has base salary S(F)
        var target = world.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
        target.Strength = 30;
        target.SalaryPerRound = MarketAndEconomyEngine.CalculateFairSalaryPerRound(world, target, seller);
        Assert.Equal(3_150m, target.SalaryPerRound);

        // 2. Existing player's negotiated salary is preserved when strength evolves
        target.SalaryPerRound = 2_800m;
        target.SeasonGoals = 12;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(world);
        Assert.Equal(31, target.Strength);
        Assert.Equal(2_800m, target.SalaryPerRound);
        Assert.Equal(ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(31, world.Economy.InflationIndex, 1.0, 1),
            MarketAndEconomyEngine.CalculateFairSalaryPerRound(world, target, seller));

        // 3. Salary after signing uses MarketContractTermsEngine.SigningSalary(world, player, buyer)
        target.IsListedForTransfer = true;
        target.AskingPrice = 200_000m;
        decimal expectedSigningSalary = MarketContractTermsEngine.SigningSalary(world, target, buyer);
        Assert.Equal(3_300m, expectedSigningSalary);
        var transfer = MarketAndEconomyEngine.ExecuteTransfer(
            world, buyer.Id, seller.Id, target.Id, 200_000m, "Compra clássica", "buy-02c");
        Assert.True(transfer.Success);
        Assert.Equal(expectedSigningSalary, target.SalaryPerRound);

        // 4. Salary after voluntary renewal (Stage 02B preserved)
        var renewPlayer = world.Players.First(p => p.ClubId == buyer.Id && p.Id != target.Id);
        renewPlayer.IsListedForTransfer = false;
        renewPlayer.Strength = 35;
        renewPlayer.SalaryPerRound = 2_000m;
        var quote = ClassicEconomyEngine.PreviewRenewal(world, buyer, renewPlayer);
        Assert.True(quote.RequiresRaise);
        Assert.Equal(2_200m, quote.RequiredSalary);
        Assert.True(ClassicEconomyEngine.RenewContract(world, buyer, renewPlayer, 0, "renew-02c", termsToken: quote.TermsToken).Success);
        Assert.Equal(2_200m, renewPlayer.SalaryPerRound);
        Assert.True(ClassicEconomyEngine.IsSeasonRenewed(world, renewPlayer));
        Assert.False(MarketValidationEngine.ValidateSale(world, buyer.Id, renewPlayer.Id).Success);

        // 5. Salary after spontaneous raise request (Stage 02B preserved: accept updates wage; refuse creates mandatory auction at R$ 0)
        var raisePlayer = world.Players.Last(p => p.ClubId == seller.Id && !ClassicEconomyEngine.IsSeasonRenewed(world, p));
        raisePlayer.IsListedForTransfer = false;
        raisePlayer.ContractRoundsRemaining = 0;
        raisePlayer.SalaryPerRound = 1_500m;
        var req = new SalaryRaiseRequest
        {
            Id = Guid.NewGuid(),
            WorldId = world.Id,
            ClubId = seller.Id,
            PlayerId = raisePlayer.Id,
            Season = world.SeasonNumber,
            Round = world.RoundNumber,
            CurrentSalary = 1_500m,
            RequestedSalary = 1_650m,
            ReferenceSalary = MarketAndEconomyEngine.CalculateFairSalaryPerRound(world, raisePlayer, seller),
            RenewalMatches = 20
        };
        world.Economy.SalaryRequests.Add(req);
        Assert.True(ClassicEconomyEngine.RespondSalary(world, seller, req.Id, true, "accept-raise-02c").Success);
        Assert.Equal(1_650m, raisePlayer.SalaryPerRound);
    }

    [Fact]
    public void MarketValueFormula_Strengths1To50_AndControlledBehaviorComparison()
    {
        // Neutral modifier (A = 0, B = 2, D = 2 => 1 - 0 - 0.04 + 0.04 = 1.00)
        foreach (var row in ExpectedStrengthTable)
        {
            decimal v = ClassicPlayerEconomyEngine.CalculateClassicalMarketValue(
                row.Strength, PlayerBehavior.FairPlay, modifierB: 2, modifierD: 2, inflationIndex: 2.0, currencyFactorC: 1);
            Assert.Equal(row.ClassicalMarketValueNeutralC1, v);
        }

        // Controlled behavior comparison: same F=35, same club, same I=2.0, same B=2, D=2, varying only Behavior (0..5)
        // Base = 2000 * 1 * 2.0 * 35^1.04 = 161394.889...
        decimal vFairPlay = ClassicPlayerEconomyEngine.CalculateClassicalMarketValue(35, PlayerBehavior.FairPlay, 2, 2, 2.0, 1);       // 1.00
        decimal vCordeirinho = ClassicPlayerEconomyEngine.CalculateClassicalMarketValue(35, PlayerBehavior.Cordeirinho, 2, 2, 2.0, 1); // 0.96 (-4%)
        decimal vCavalheiro = ClassicPlayerEconomyEngine.CalculateClassicalMarketValue(35, PlayerBehavior.Cavalheiro, 2, 2, 2.0, 1);   // 0.92 (-8%)
        decimal vCaneleiro = ClassicPlayerEconomyEngine.CalculateClassicalMarketValue(35, PlayerBehavior.Caneleiro, 2, 2, 2.0, 1);     // 0.88 (-12%)
        decimal vCaceteiro = ClassicPlayerEconomyEngine.CalculateClassicalMarketValue(35, PlayerBehavior.Caceteiro, 2, 2, 2.0, 1);     // 0.84 (-16%)
        decimal vSarrafeiro = ClassicPlayerEconomyEngine.CalculateClassicalMarketValue(35, PlayerBehavior.Sarrafeiro, 2, 2, 2.0, 1);   // 0.80 (-20%)

        Assert.Equal(161_395m, vFairPlay);
        Assert.Equal(154_939m, vCordeirinho);
        Assert.Equal(148_484m, vCavalheiro);
        Assert.Equal(142_028m, vCaneleiro);
        Assert.Equal(135_572m, vCaceteiro);
        Assert.Equal(129_116m, vSarrafeiro);

        // Behavior does NOT change calculated base salary S in ELIFOOT.EXE (SEG03:7FF4)
        var world = WorldSeedFactory.CreateSeededWorld(totalClubs: 8);
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(world, 1, true);
        var p1 = new Player { Name = "Jogador Controle", Strength = 35, Position = PlayerPosition.M, Behavior = PlayerBehavior.FairPlay, PersonalModifierB = 2, PersonalModifierD = 2 };
        var p2 = new Player { Name = "Jogador Controle", Strength = 35, Position = PlayerPosition.M, Behavior = PlayerBehavior.Sarrafeiro, PersonalModifierB = 2, PersonalModifierD = 2 };

        Assert.Equal(
            MarketAndEconomyEngine.CalculateFairSalaryPerRound(world, p1),
            MarketAndEconomyEngine.CalculateFairSalaryPerRound(world, p2));
        Assert.True(MarketAndEconomyEngine.CalculateFairValue(world, p1) > MarketAndEconomyEngine.CalculateFairValue(world, p2));
    }

    [Fact]
    public void AsteriskComparison_Strength40WithAndWithoutStar_AffectsAiHiringInterestByTenPercent()
    {
        var world = WorldSeedFactory.CreateSeededWorld(totalClubs: 8);
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(world, currencyFactorC: 1, recalculateInitialPlayerValues: true);

        var seller = world.Clubs[0];
        var buyer = world.Clubs[1];
        seller.Country = "Brasil";
        buyer.Country = "Brasil";

        // Controlled comparison: identical F=40, Behavior=Cavalheiro (A=2), B=4, D=8, Nationality=BR; only IsStar differs
        var playerA = new Player
        {
            Id = Guid.NewGuid(),
            GameWorldId = world.Id,
            ClubId = seller.Id,
            Name = "Craque Controle",
            Position = PlayerPosition.A,
            Strength = 40,
            Behavior = PlayerBehavior.Cavalheiro,
            PersonalModifierB = 4,
            PersonalModifierD = 8,
            IsStar = false,
            Nationality = "Brasil",
            SalaryPerRound = 4_300m
        };

        var playerB = new Player
        {
            Id = Guid.NewGuid(),
            GameWorldId = world.Id,
            ClubId = seller.Id,
            Name = "Craque Controle",
            Position = PlayerPosition.A,
            Strength = 40,
            Behavior = PlayerBehavior.Cavalheiro,
            PersonalModifierB = 4,
            PersonalModifierD = 8,
            IsStar = true,
            Nationality = "Brasil",
            SalaryPerRound = 4_300m
        };

        var econA = ClassicPlayerEconomyEngine.EvaluatePlayerEconomy(world, playerA, buyer, seller);
        var econB = ClassicPlayerEconomyEngine.EvaluatePlayerEconomy(world, playerB, buyer, seller);

        // In ELIFOOT.EXE, with A, B, D held constant:
        // - Base salary S (SEG03:7FF4) is identical (4,300)
        // - Market valuation V (SEG09:371A) is identical (185,440)
        // - Candidate price Q (SEG03:63FA) is identical (344,704)
        // - AI hiring interest (SEG03:5BB1..5DAE) increases by exactly 10% (1.50 -> 1.65)
        Assert.Equal(4_300m, econA.CalculatedBaseSalary);
        Assert.Equal(4_300m, econB.CalculatedBaseSalary);
        Assert.Equal(185_440m, econA.IndividualMarketValue);
        Assert.Equal(185_440m, econB.IndividualMarketValue);
        Assert.Equal(344_704m, econA.CandidateSigningPrice);
        Assert.Equal(344_704m, econB.CandidateSigningPrice);
        Assert.Equal(1.50m, econA.AiHiringInterestFactor);
        Assert.Equal(1.65m, econB.AiHiringInterestFactor);
        Assert.Equal(Math.Round(econA.AiHiringInterestFactor * 1.10m, 4), econB.AiHiringInterestFactor);
    }

    [Fact]
    public void NationalityScenarios_A_To_E_VerifyCandidatePriceAndAiInterestWithoutCreatingNewForeignerQuota()
    {
        var world = WorldSeedFactory.CreateSeededWorld(totalClubs: 16);
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(world, currencyFactorC: 1, recalculateInitialPlayerValues: true);

        var buyerBr = world.Clubs[0]; buyerBr.Country = "Brasil";
        var buyerPt = world.Clubs[1]; buyerPt.Country = "Portugal";
        var buyerIt = world.Clubs[2]; buyerIt.Country = "Itália";
        var seller = world.Clubs[3]; seller.Country = "Brasil";

        var playerBr = new Player
        {
            ClubId = seller.Id, Name = "Ronaldo", Position = PlayerPosition.A, Strength = 40,
            Behavior = PlayerBehavior.FairPlay, PersonalModifierB = 2, PersonalModifierD = 2,
            IsStar = false, Nationality = "Brasil"
        };
        var playerAr = new Player
        {
            ClubId = seller.Id, Name = "Batistuta", Position = PlayerPosition.A, Strength = 40,
            Behavior = PlayerBehavior.FairPlay, PersonalModifierB = 2, PersonalModifierD = 2,
            IsStar = false, Nationality = "Argentina"
        };
        var playerPt = new Player
        {
            ClubId = seller.Id, Name = "Figo", Position = PlayerPosition.M, Strength = 40,
            Behavior = PlayerBehavior.FairPlay, PersonalModifierB = 2, PersonalModifierD = 2,
            IsStar = false, Nationality = "Portugal"
        };

        // Base market value V is identical for all three (185,440)
        decimal v = ClassicPlayerEconomyEngine.CalculateMarketValue(world, playerBr);
        Assert.Equal(185_440m, v);
        Assert.Equal(v, ClassicPlayerEconomyEngine.CalculateMarketValue(world, playerAr));
        Assert.Equal(v, ClassicPlayerEconomyEngine.CalculateMarketValue(world, playerPt));

        // Scenario A: Brazilian signed by Brazilian club (SameCountry: (Q + 30000) * 1.60)
        decimal qBrByBr = ClassicPlayerEconomyEngine.CalculateCandidateSigningPrice(world, playerBr, buyerBr, seller);
        Assert.Equal(Math.Round((v + 30_000m) * 1.60m, 0, MidpointRounding.ToEven), qBrByBr);
        Assert.Equal(344_704m, qBrByBr);

        // Scenario B: Foreigner (Argentine) signed by Brazilian club (Foreigner: Q without nationality surcharge)
        decimal qArByBr = ClassicPlayerEconomyEngine.CalculateCandidateSigningPrice(world, playerAr, buyerBr, seller);
        Assert.Equal(v, qArByBr);
        Assert.Equal(185_440m, qArByBr);

        // Scenario C: Equivalent nationality — PLOP (Portuguese signed by Brazilian club) & Bosman (Portuguese signed by Italian club)
        decimal qPtByBr = ClassicPlayerEconomyEngine.CalculateCandidateSigningPrice(world, playerPt, buyerBr, seller);
        decimal qPtByIt = ClassicPlayerEconomyEngine.CalculateCandidateSigningPrice(world, playerPt, buyerIt, seller);
        decimal expectedEquiv = Math.Round((v + 15_000m) * 1.30m, 0, MidpointRounding.ToEven);
        Assert.Equal(expectedEquiv, qPtByBr);
        Assert.Equal(expectedEquiv, qPtByIt);
        Assert.Equal(260_572m, qPtByBr);

        // Scenario D: Same player (Portuguese) with buyers from Portugal (Same), Brazil/Italy (Equivalent), and Argentina (Foreigner)
        var buyerAr = world.Clubs[4]; buyerAr.Country = "Argentina";
        Assert.Equal(344_704m, ClassicPlayerEconomyEngine.CalculateCandidateSigningPrice(world, playerPt, buyerPt, seller));
        Assert.Equal(260_572m, ClassicPlayerEconomyEngine.CalculateCandidateSigningPrice(world, playerPt, buyerBr, seller));
        Assert.Equal(185_440m, ClassicPlayerEconomyEngine.CalculateCandidateSigningPrice(world, playerPt, buyerAr, seller));

        // Base salary does NOT change with nationality
        Assert.Equal(
            ClassicPlayerEconomyEngine.CalculateReferenceSalaryPerRound(world, playerBr, buyerBr),
            ClassicPlayerEconomyEngine.CalculateReferenceSalaryPerRound(world, playerAr, buyerBr));

        // Scenario E: Buyer squad with different numbers of foreigners (N = 0, 2, 5)
        var buyerSquad = world.Players.Where(p => p.ClubId == buyerBr.Id).ToList();
        foreach (var p in buyerSquad) p.Nationality = "Brasil";
        Assert.Equal(0, ClassicPlayerEconomyEngine.CountForeignersInSquad(world, buyerBr));
        Assert.Equal(1.50m, ClassicPlayerEconomyEngine.CalculateAiHiringInterestFactor(world, playerBr, buyerBr, seller));
        Assert.Equal(1.15m, ClassicPlayerEconomyEngine.CalculateAiHiringInterestFactor(world, playerPt, buyerBr, seller));
        Assert.Equal(1.00m, ClassicPlayerEconomyEngine.CalculateAiHiringInterestFactor(world, playerAr, buyerBr, seller));

        buyerSquad[0].Nationality = "Argentina";
        buyerSquad[1].Nationality = "Uruguai";
        Assert.Equal(2, ClassicPlayerEconomyEngine.CountForeignersInSquad(world, buyerBr));
        Assert.Equal(1.60m, ClassicPlayerEconomyEngine.CalculateAiHiringInterestFactor(world, playerBr, buyerBr, seller));
        Assert.Equal(1.25m, ClassicPlayerEconomyEngine.CalculateAiHiringInterestFactor(world, playerPt, buyerBr, seller));
        Assert.Equal(1.00m, ClassicPlayerEconomyEngine.CalculateAiHiringInterestFactor(world, playerAr, buyerBr, seller));

        for (int i = 0; i < 5; i++) buyerSquad[i].Nationality = "Argentina";
        Assert.Equal(5, ClassicPlayerEconomyEngine.CountForeignersInSquad(world, buyerBr));
        Assert.Equal(1.75m, ClassicPlayerEconomyEngine.CalculateAiHiringInterestFactor(world, playerBr, buyerBr, seller));
        Assert.Equal(1.40m, ClassicPlayerEconomyEngine.CalculateAiHiringInterestFactor(world, playerPt, buyerBr, seller));
        Assert.Equal(1.00m, ClassicPlayerEconomyEngine.CalculateAiHiringInterestFactor(world, playerAr, buyerBr, seller));

        // Confirm no artificial foreigner roster limit blocks a legal purchase
        var sellerForeignPlayer = world.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
        sellerForeignPlayer.Nationality = "Argentina";
        Assert.True(MarketValidationEngine.ValidatePurchase(world, buyerBr.Id, seller.Id, sellerForeignPlayer.Id, 185_440m).Success);

        // Verify SEG03:6558..6640 price quantization steps
        Assert.Equal(2_100_000m, ClassicPlayerEconomyEngine.QuantizeClassicalPrice(2_142_000m)); // >= 2M -> 100k step
        Assert.Equal(1_250_000m, ClassicPlayerEconomyEngine.QuantizeClassicalPrice(1_238_000m)); // >= 1M -> 50k step
        Assert.Equal(675_000m, ClassicPlayerEconomyEngine.QuantizeClassicalPrice(681_200m));     // > 500k -> 25k step
        Assert.Equal(340_000m, ClassicPlayerEconomyEngine.QuantizeClassicalPrice(344_704m));     // > 100k -> 10k step
        Assert.Equal(84_000m, ClassicPlayerEconomyEngine.QuantizeClassicalPrice(84_320m));       // <= 100k -> 1k step
    }

    [Fact]
    public void InflationIndex_InitialBoundsEvolutionReproducibilityAndPersistence()
    {
        var world = WorldSeedFactory.CreateSeededWorld(seed: 199801, totalClubs: 16);
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(world, currencyFactorC: 1, recalculateInitialPlayerValues: true);

        // Initial index = 2.0, display = 20
        Assert.Equal(2.0, world.Economy.InflationIndex);
        Assert.Equal(20, world.Economy.InflationDisplayIndex);
        Assert.Equal(1998, world.Economy.EconomyYearCounter);

        // Y % 100 >= 50 (e.g. 1998 % 100 = 98 >= 50): delta = 0.01 * (U1 - U2 - U3)
        var (next1998, delta1998) = ClassicPlayerEconomyEngine.AdvanceInflationIndex(2.0, 1998, 0.20, 0.80, 0.70);
        Assert.Equal(0.01 * (0.20 - 0.80 - 0.70), delta1998, 10);
        Assert.Equal(2.0 + delta1998, next1998, 10);

        // Y % 100 < 50 (e.g. 2000 % 100 = 0 < 50): delta = 0.01 * (U1 + U2 - U3)
        var (next2000, delta2000) = ClassicPlayerEconomyEngine.AdvanceInflationIndex(2.0, 2000, 0.80, 0.70, 0.20);
        Assert.Equal(0.01 * (0.80 + 0.70 - 0.20), delta2000, 10);
        Assert.Equal(2.0 + delta2000, next2000, 10);

        // Bounds [0.5, 10.0] and display [5, 100]
        var (clampedLow, _) = ClassicPlayerEconomyEngine.AdvanceInflationIndex(0.501, 1998, 0.0, 1.0, 1.0);
        Assert.Equal(0.5, clampedLow);
        Assert.Equal(5, ClassicPlayerEconomyEngine.CalculateInflationDisplay(clampedLow));

        var (clampedHigh, _) = ClassicPlayerEconomyEngine.AdvanceInflationIndex(9.995, 2005, 1.0, 1.0, 0.0);
        Assert.Equal(10.0, clampedHigh);
        Assert.Equal(100, ClassicPlayerEconomyEngine.CalculateInflationDisplay(clampedHigh));

        // Deterministic season-end update and effect on S and V without touching stadium/gate economics
        var home = world.Clubs[0];
        var away = world.Clubs[1];
        var gateBefore = MarketAndEconomyEngine.CalculateMatchGateEconomics(home, away, CompetitionType.League, world.Seed, 1, 1, 1);

        var samplePlayer = world.Players[0];
        samplePlayer.Strength = 40;
        samplePlayer.Behavior = PlayerBehavior.FairPlay;
        samplePlayer.PersonalModifierB = 2;
        samplePlayer.PersonalModifierD = 2;

        world.Economy.InflationIndex = 3.0; // Display 30
        decimal sAt3 = ClassicPlayerEconomyEngine.CalculateReferenceSalaryPerRound(world, samplePlayer, home);
        decimal vAt3 = ClassicPlayerEconomyEngine.CalculateMarketValue(world, samplePlayer);
        Assert.Equal(6_400m, sAt3);
        Assert.Equal(278_159m, vAt3);

        // Gate revenue is untouched by inflation index
        var gateAfter = MarketAndEconomyEngine.CalculateMatchGateEconomics(home, away, CompetitionType.League, world.Seed, 1, 1, 1);
        Assert.Equal(gateBefore, gateAfter);

        // Reproducibility across two worlds with same seed and persistence through JSON round-trip
        var w1 = WorldSeedFactory.CreateSeededWorld(seed: 199877, totalClubs: 8);
        var w2 = WorldSeedFactory.CreateSeededWorld(seed: 199877, totalClubs: 8);
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(w1, 1, true);
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(w2, 1, true);

        for (int s = 1; s <= 10; s++)
        {
            var e1 = ClassicPlayerEconomyEngine.AdvanceWorldInflationAtSeasonEnd(w1, s);
            var e2 = ClassicPlayerEconomyEngine.AdvanceWorldInflationAtSeasonEnd(w2, s);
            Assert.Equal(e1.InflationAfter, e2.InflationAfter);
            Assert.InRange(w1.Economy.InflationIndex, 0.5, 10.0);
        }

        var reloaded = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w1))!;
        Assert.Equal(w1.Economy.InflationIndex, reloaded.Economy.InflationIndex);
        Assert.Equal(w1.Economy.EconomyYearCounter, reloaded.Economy.EconomyYearCounter);
        Assert.Equal(10, reloaded.Economy.InflationHistory.Count);
    }

    [Fact]
    public void NameTransformation_DeobfuscatesEftBytes_AndDerivesModifiersABDAndStarByPosition()
    {
        string plainName = "RONALDO";
        byte[] obfuscated = ClassicPlayerEconomyEngine.ObfuscateEftName(plainName);
        Assert.Equal(plainName.Length, obfuscated[0]);
        Assert.NotEqual((byte)'O', obfuscated[2]); // Obfuscated in .EFT format
        string restored = ClassicPlayerEconomyEngine.DeobfuscateEftName(obfuscated);
        Assert.Equal(plainName, restored);

        // Verify SEG03:7516 byte sum H and position dependence:
        // Defenders (PlayerPosition.D) shift Behavior A by (A_raw + 2) % 6, and only M/A with D >= 8 get IsStar = true
        var attrM = ClassicPlayerEconomyEngine.DerivePersonalAttributesFromName(plainName, PlayerPosition.M);
        var attrD = ClassicPlayerEconomyEngine.DerivePersonalAttributesFromName(plainName, PlayerPosition.D);
        var attrG = ClassicPlayerEconomyEngine.DerivePersonalAttributesFromName(plainName, PlayerPosition.G);

        int expectedH = plainName.Sum(ch => (int)(byte)ch);
        Assert.Equal(expectedH, attrM.ByteSumH);
        Assert.Equal((expectedH % 10) + 1, attrM.ModifierD);
        Assert.Equal(expectedH % 11, attrM.ModifierB);
        Assert.Equal(((int)attrM.BehaviorA + 2) % 6, (int)attrD.BehaviorA);
        Assert.False(attrD.IsStar);
        Assert.False(attrG.IsStar);

        // Trailing '*' is ignored when hashing name, matching SEG03:7516 clearing +0x1A before calling SEG12:13FE
        var attrWithStar = ClassicPlayerEconomyEngine.DerivePersonalAttributesFromName("RONALDO*", PlayerPosition.M);
        Assert.Equal(attrM, attrWithStar);
    }

    [Fact]
    public void ExistingSaves_KeepLegacyPolicySalariesAndBalancesWithoutSilentAlteration()
    {
        var legacyWorld = WorldSeedFactory.CreateSeededWorld(seed: 199801, totalClubs: 32);
        Assert.Equal(ClassicPlayerEconomyEngine.CalibratedMultiplayerPolicy, legacyWorld.Economy.PlayerEconomyRules.Policy);

        var salariesBefore = legacyWorld.Players.ToDictionary(p => p.Id, p => p.SalaryPerRound);
        var pricesBefore = legacyWorld.Players.ToDictionary(p => p.Id, p => p.MarketPrice);
        var cashBefore = legacyWorld.Clubs.ToDictionary(c => c.Id, c => c.CashBalance);

        string json = JsonSerializer.Serialize(legacyWorld);
        var restored = JsonSerializer.Deserialize<GameWorld>(json)!;
        ClassicEconomyEngine.EnsureMigrated(restored);

        Assert.All(restored.Players, p =>
        {
            Assert.Equal(salariesBefore[p.Id], p.SalaryPerRound);
            Assert.Equal(pricesBefore[p.Id], p.MarketPrice);
            Assert.Equal(MarketAndEconomyEngine.CalculateFairValue(p), MarketAndEconomyEngine.CalculateFairValue(restored, p));
        });
        Assert.All(restored.Clubs, c => Assert.Equal(cashBefore[c.Id], c.CashBalance));
    }
}
