using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Elifoot.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public sealed class Integration014ATests
{
    [Theory]
    [InlineData(FootballGender.Male, false)] [InlineData(FootballGender.Female, false)]
    [InlineData(FootballGender.Male, true)] [InlineData(FootballGender.Female, true)]
    public void ImportedFoundationUsesApprovedEconomyForFinalSquadsAndIndividualCredit(FootballGender gender, bool manual)
    {
        var world = Imported014AWorlds.Create(gender, manual: manual);
        var baseline = WorldSeedFactory.CreateSeededWorld(forceEvolutionMode: ForceEvolutionMode.ClassicAdapted);
        Assert.Equal(128, world.Clubs.Count); Assert.Equal(8, world.CatalogOrigin!.Countries.Count);
        Assert.Equal(138800000m, world.Clubs.Sum(c => c.CashBalance));
        Assert.Equal(OperatingEconomyEngine.CurrentPolicy, world.Economy.OperatingRules.Name);
        Assert.Empty(world.BankLoans); Assert.Empty(world.Auctions);
        Assert.Equal(128, world.Transactions.Count); Assert.All(world.Transactions, t => Assert.Equal(FinancialTransactionCategory.InitialBalance, t.Category));
        Assert.Equal(world.Players.Count, world.Players.Select(p => p.Id).Distinct().Count());
        Assert.Equal(world.Players.Count, world.ClassicForce.SeasonStartingStrengths.Count);
        for (int i = 0; i < 128; i++)
        {
            var club = world.Clubs[i]; var expected = baseline.Clubs[i];
            Assert.Equal(expected.DivisionTier, club.DivisionTier); Assert.Equal(expected.CashBalance, club.CashBalance);
            Assert.Equal(expected.Stadium.BuiltCapacity, club.Stadium.BuiltCapacity);
            var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
            Assert.True(SquadValidator.ValidateClubSquadLimits(squad).IsValid);
            Assert.All(squad, p => { Assert.InRange(p.Strength, 1, 50); Assert.True(p.SalaryPerRound > 0); Assert.InRange(p.ContractRoundsRemaining, 12, 27);
                Assert.Equal(MarketAndEconomyEngine.CalculateFairValue(p), p.MarketPrice); Assert.Equal(MarketAndEconomyEngine.CalculateFairSalaryPerRound(p), p.SalaryPerRound); });
            Assert.Equal(ClassicEconomyEngine.AssessCredit(baseline, expected).LoanLimit, ClassicEconomyEngine.AssessCredit(world, club).LoanLimit);
            Assert.NotNull(club.LatestSquadPlan); Assert.All(club.PreferredStarterIds.Concat(club.PreferredBenchIds), id => Assert.Contains(squad, p => p.Id == id));
        }
        Assert.Equal(new[] { 1, 2 }, world.Clubs.Where(c => c.DivisionTier == DivisionTier.QuartaDivisao)
            .Select(c => ClassicEconomyEngine.AssessCredit(world, c).LoanLimit).Distinct().Order().ToArray());
    }

    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public void ImportedInsolvencyKeepsUnsoldPlayerAndProtectsTheLastGoalkeeperAndTenOutfield(FootballGender gender)
    {
        var world = Imported014AWorlds.Create(gender); var club = world.Clubs[0];
        MarketAndEconomyEngine.TryRecordTransaction(world, club, FinancialTransactionCategory.StadiumRenovation,
            -club.CashBalance - 1000, "Controlled 014A insolvency test expense", "014a-crisis-probe");
        var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
        var expected = squad.Where(p => SquadValidator.CanRemovePlayerFromSquad(squad, p.Id).CanRemove)
            .OrderByDescending(p => p.Strength).ThenByDescending(p => p.SalaryPerRound).ThenBy(p => p.Id).First();
        ClassicEconomyEngine.EvaluateInsolvency(world); var auction = Assert.Single(world.Auctions);
        Assert.Equal(expected.Id, auction.PlayerId); Assert.Equal(0, auction.MinimumBid); Assert.Equal(club.Id, expected.ClubId);
        world.RoundState = RoundState.RESULTS;
        world.SettledFinancialRounds.Add($"round_settled:S{world.SeasonNumber}:R{world.RoundNumber}");
        decimal before = club.CashBalance; ClassicEconomyEngine.SettleSpecialAuction(world, auction);
        Assert.Equal(before, club.CashBalance); Assert.Equal(club.Id, expected.ClubId); Assert.True(auction.IsClosed);
        var keep = squad.Where(p => p.Position == PlayerPosition.G).Take(1).Concat(squad.Where(p => p.Position != PlayerPosition.G).Take(10)).Select(p => p.Id).ToHashSet();
        world.Players.RemoveAll(p => p.ClubId == club.Id && !keep.Contains(p.Id)); world.RoundNumber += 3;
        ClassicEconomyEngine.EvaluateInsolvency(world); ClassicEconomyEngine.EvaluateInsolvency(world);
        Assert.Single(world.Auctions); Assert.Equal(-1000, club.CashBalance);
        Assert.Single(world.Economy.CrisisHistory, h => h.ClubId == club.Id && h.Kind == "NO_ELIGIBLE_PLAYER");
        Assert.True(SquadValidator.ValidateClubSquadLimits(world.Players.Where(p => p.ClubId == club.Id).ToList()).IsValid);
    }

    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public void CatalogPackageRoundTripRetainsClassicForceProtectedSalaryAndApprovedCommercialRules(FootballGender gender)
    {
        var world = Imported014AWorlds.Create(gender); var player = world.Players[0];
        decimal negotiated = player.SalaryPerRound + 123.45m;
        Assert.True(ClassicEconomyEngine.RenewContract(world, world.Clubs[0], player, 5, "014a-package-contract", negotiated).Success);
        var exported = WorldEditorAndPackageService.ExportWorldToPackage(world);
        var json = WorldEditorAndPackageService.SerializePackageToJson(exported);
        var decoded = JsonSerializer.Deserialize<WorldPackageExportDto>(json, CatalogImportCodec.Json)!;
        var result = WorldEditorAndPackageService.CreateWorldFromPackage(decoded, "Cópia de configuração 014A");
        Assert.True(result.Validation.IsValid); var clone = result.World!;
        Assert.Equal(ForceEvolutionMode.ClassicAdapted, clone.ForceEvolutionMode);
        Assert.Equal(OperatingEconomyEngine.CurrentPolicy, clone.Economy.OperatingRules.Name);
        Assert.Equal(gender, clone.Gender); Assert.Equal(world.CatalogOrigin!.DataHash, clone.CatalogOrigin!.DataHash);
        var copied = clone.Players.Single(p => p.Id == player.Id);
        Assert.Equal(negotiated, copied.SalaryPerRound); Assert.True(copied.NegotiatedContractProtected);
        Assert.Equal(player.ContractRoundsRemaining, copied.ContractRoundsRemaining);
        Assert.Equal(world.Clubs.Sum(c => c.CashBalance), clone.Clubs.Sum(c => c.CashBalance));
        Assert.Equal(128, clone.Transactions.Count); Assert.Equal(clone.Clubs.Sum(c => c.CashBalance), clone.Transactions.Sum(t => t.Amount));
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(clone, []);
        Assert.Equal(Math.Round(185000m/clone.TotalRoundsInSeason,2), clone.Transactions.Single(t => t.ClubId == clone.Clubs[0].Id && t.Category == FinancialTransactionCategory.Sponsorship).Amount);
        Assert.Equal(new[] {1,2}, clone.Clubs.Where(c => c.DivisionTier == DivisionTier.QuartaDivisao).Select(c => ClassicEconomyEngine.AssessCredit(clone,c).LoanLimit).Distinct().Order());
        var oldJson = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        oldJson.Remove("forceEvolutionMode");
        foreach (var item in oldJson["players"]!.AsArray()) item!.AsObject().Remove("negotiatedContractProtected");
        var oldPackage = JsonSerializer.Deserialize<WorldPackageExportDto>(oldJson.ToJsonString(), CatalogImportCodec.Json)!;
        var restoredOldCatalog = WorldEditorAndPackageService.CreateWorldFromPackage(oldPackage, "Pacote 013A antigo").World!;
        Assert.Equal(ForceEvolutionMode.ClassicAdapted, restoredOldCatalog.ForceEvolutionMode);
        var oldPlayer = restoredOldCatalog.Players.Single(p => p.Id == player.Id);
        Assert.Equal(negotiated, oldPlayer.SalaryPerRound); Assert.True(oldPlayer.NegotiatedContractProtected);
        var restoredGeneric = WorldEditorAndPackageService.CreateWorldFromPackage(oldPackage with { CatalogOrigin = null }, "Pacote genérico antigo").World!;
        Assert.Equal(ForceEvolutionMode.LegacySeasonal, restoredGeneric.ForceEvolutionMode);
        Assert.Equal(negotiated, restoredGeneric.Players.Single(p => p.Id == player.Id).SalaryPerRound);
    }

    private static ServiceProvider Provider(string? catalogConnection = null, string? worldConnection = null)
    {
        string catalog = catalogConnection ?? Environment.GetEnvironmentVariable("ELIFOOT_014A_CATALOG_PG") ?? throw new InvalidOperationException("Execute o runner PostgreSQL 014A.");
        string worlds = worldConnection ?? Environment.GetEnvironmentVariable("ELIFOOT_014A_WORLD_PG")!;
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(catalog);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.True(parsed.Port==55442 && parsed.Database!.StartsWith("elifoot_016b_",StringComparison.Ordinal) || parsed.Port==55441 && parsed.Database!.StartsWith("elifoot_016a_",StringComparison.Ordinal) || parsed.Port==55440 && parsed.Database!.StartsWith("elifoot_015b_",StringComparison.Ordinal) || parsed.Port==55439 && parsed.Database!.StartsWith("elifoot_015a_",StringComparison.Ordinal) || parsed.Port==55438 && parsed.Database!.StartsWith("elifoot_014c_",StringComparison.Ordinal) || parsed.Port==55435 && parsed.Database!.StartsWith("elifoot_014a_",StringComparison.Ordinal) || parsed.Port==55436 && parsed.Database!.StartsWith("elifoot_013c_",StringComparison.Ordinal) || parsed.Port==55437 && parsed.Database!.StartsWith("elifoot_014b_",StringComparison.Ordinal));
        Assert.NotEqual(catalog, worlds);
        var services = new ServiceCollection(); services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(catalog));
        services.AddDbContext<ElifootDbContext>(o => o.UseNpgsql(worlds));
        services.AddSingleton<IWorldRepository, EfWorldRepository>(); services.AddSingleton(new CatalogSyncOptions());
        services.AddSingleton<CatalogSyncService>(); services.AddSingleton<WorldCreatorService>(); return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PostgresCreatedWorldsPreserveContractsDebtTradeCashAndSnapshotsAcrossRepositoryRestart()
    {
        // Fresh databases per execution: tests never downgrade or reuse the live catalog.
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var catalogConnection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ELIFOOT_014A_CATALOG_PG"));
        var worldConnection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ELIFOOT_014A_WORLD_PG"));
        string prefix=catalogConnection.Port==55442?"elifoot_016b_test_":catalogConnection.Port==55441?"elifoot_016a_test_":catalogConnection.Port==55440?"elifoot_015b_test_":catalogConnection.Port==55439?"elifoot_015a_test_":catalogConnection.Port==55438?"elifoot_014c_test_":catalogConnection.Port==55437?"elifoot_014b_test_":catalogConnection.Port==55436?"elifoot_013c_test_":"elifoot_014a_test_";
        catalogConnection.Database=prefix+"catalog_"+suffix;worldConnection.Database=prefix+"worlds_"+suffix;
        await using var provider = Provider(catalogConnection.ConnectionString, worldConnection.ConnectionString);
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        }
        var sync = provider.GetRequiredService<CatalogSyncService>();
        foreach (var package in CatalogFixtureFactory.Packages()) await sync.ImportAsync(package);
        var creator = provider.GetRequiredService<WorldCreatorService>(); var repo = provider.GetRequiredService<IWorldRepository>();
        foreach (var gender in Enum.GetValues<FootballGender>())
        {
            var owner = Guid.NewGuid(); var request = Imported014AWorlds.Request(gender);
            Assert.Equal(160, (await creator.Select(request.Countries, gender, "2026", true)).Clubs.Count);
            var preview = await creator.Preview(owner, request); Assert.Empty(preview.Errors);
            var world = await creator.Confirm(owner, preview.DraftId); world.RoundNumber = 1;
            var orchestrator = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);
            var sellerCoach = Guid.NewGuid(); var buyerCoach = Guid.NewGuid(); var seller = world.Clubs[1]; var buyer = world.Clubs[0];
            Assert.True((await orchestrator.JoinWorldAndAssignClubAsync(world.Id, sellerCoach, "Vendedor", "014a-seller", seller.Id)).Success);
            Assert.True((await orchestrator.JoinWorldAndAssignClubAsync(world.Id, buyerCoach, "Comprador", "014a-buyer", buyer.Id)).Success);
            var service = new ClubAndMarketApplicationService(repo, NullMatchRoomNotificationPublisher.Instance);
            var target = world.Players.Last(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
            decimal price = MarketAndEconomyEngine.CalculateFairValue(target); decimal sellerBefore = seller.CashBalance;
            Assert.True((await service.ListPlayerForTransferAsync(world.Id, sellerCoach, target.Id, price, idempotencyKey: "014a-list")).Success);
            Assert.True((await service.BuyListedPlayerAsync(world.Id, buyerCoach, target.Id, "014a-buy",salaryTermsToken:MarketContractTermsEngine.SalaryTermsToken(world,target))).Success);
            Assert.Equal(sellerBefore + price, seller.CashBalance); Assert.Equal(buyer.Id, target.ClubId);
            var renewal = world.Players.First(p => p.ClubId == buyer.Id);
            var renewalQuote=ClassicEconomyEngine.PreviewRenewal(world,buyer,renewal);
            decimal salary = renewalQuote.RequiredSalary;
            Assert.True((await service.RenewPlayerContractAsync(world.Id, buyerCoach, renewal.Id, 5, idempotencyKey: "014a-renew",renewalTermsToken:renewalQuote.TermsToken)).Success);
            Assert.Equal(salary, renewal.SalaryPerRound);
            Assert.Equal(world.TotalRoundsInSeason - world.RoundNumber + 1, renewal.ContractRoundsRemaining);
            Assert.True(ClassicEconomyEngine.IsSeasonRenewed(world, renewal));
            var takes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.TakeBankLoanAsync(world.Id, buyerCoach, 500000, 0, idempotencyKey: "014a-loan")));
            Assert.All(takes, result => Assert.True(result.Success)); var loan = Assert.Single(world.BankLoans);
            Assert.Equal(500000, loan.RemainingBalance); Assert.Equal(25000, loan.InterestPerMatch);
            var match = world.Fixtures.First(m => m.SeasonNumber == 1 && m.RoundNumber == 1 && (m.HomeClubId == buyer.Id || m.AwayClubId == buyer.Id));
            ClassicForceEvolutionEngine.PrepareRound(world);
            var home = world.Clubs.Single(c => c.Id == match.HomeClubId); var away = world.Clubs.Single(c => c.Id == match.AwayClubId);
            int minutes = MatchSimulationEngine.SimulateFullMatchReal(match, home, away,
                world.AiManagers.FirstOrDefault(a => a.Id == home.AiManagerId), world.AiManagers.FirstOrDefault(a => a.Id == away.AiManagerId),
                world.Players.Where(p => p.ClubId == home.Id).ToList(), world.Players.Where(p => p.ClubId == away.Id).ToList(),
                world.Players.ToDictionary(p => p.Id), world.Seed);
            Assert.InRange(minutes, 90, 120); Assert.True(ClassicEconomyEngine.Played(match));
            MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, [match]); decimal settled = buyer.CashBalance;
            MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, [match]); Assert.Equal(settled, buyer.CashBalance);
            Assert.Equal(25000, loan.InterestCharged); Assert.Equal(25000, loan.InterestPaid);
            Assert.True((await service.RepayBankLoanAsync(world.Id, buyerCoach, buyer.Id, loan.Id, "014a-repay")).Success);
            Assert.True(loan.IsPaidOff); Assert.Equal(500000, loan.PrincipalPaid);
            Assert.Equal(world.Transactions.Sum(t => t.Amount), world.Clubs.Sum(c => c.CashBalance));
            foreach (var transfer in world.Transactions.Where(t => t.TransferTransactionId != null).GroupBy(t => t.TransferTransactionId))
            { Assert.Equal(2, transfer.Count()); Assert.Equal(0, transfer.Sum(t => t.Amount)); }
            await repo.SaveWorldAsync(world); string before = JsonSerializer.Serialize(world, CatalogImportCodec.Json);
            var source = CatalogFixtureFactory.Packages().First(p => p.Scope.Gender == gender && p.Scope.DataType == CatalogDataType.Clubs);
            await sync.ImportAsync(source with { Version = "014a-updated", UpdatedAt = source.UpdatedAt.AddDays(2), Clubs = source.Clubs.Select(c => c with { Name = c.Name + " atualizado" }).ToList() });
            Assert.Equal(before, JsonSerializer.Serialize(world, CatalogImportCodec.Json));
            EfWorldRepository.ClearCacheForTesting(); await using var restarted = Provider(catalogConnection.ConnectionString, worldConnection.ConnectionString);
            var restored = (await restarted.GetRequiredService<IWorldRepository>().GetWorldAsync(world.Id))!;
            Assert.Equal(salary, restored.Players.Single(p => p.Id == renewal.Id).SalaryPerRound);
            Assert.True(ClassicEconomyEngine.IsSeasonRenewed(restored, restored.Players.Single(p => p.Id == renewal.Id)));
            Assert.Equal(0, restored.BankLoans.Single().RemainingBalance); Assert.Equal(25000, restored.BankLoans.Single().InterestCharged);
            Assert.Equal(buyer.CashBalance, restored.Clubs.Single(c => c.Id == buyer.Id).CashBalance);
            Assert.Equal(preview.DataHash, restored.CatalogOrigin!.DataHash); Assert.Equal(gender, restored.Gender);
            var restoredService = new ClubAndMarketApplicationService(restarted.GetRequiredService<IWorldRepository>(), NullMatchRoomNotificationPublisher.Instance);
            Assert.True((await restoredService.TakeBankLoanAsync(restored.Id, buyerCoach, 500000, 0, idempotencyKey: "014a-loan")).Success);
            Assert.Single(restored.BankLoans); Assert.Equal(0, restored.BankLoans[0].RemainingBalance);
            Assert.Equal(world.Id, (await restarted.GetRequiredService<WorldCreatorService>().Confirm(owner, preview.DraftId)).Id);
            Assert.Equal(JsonSerializer.Serialize(request, CatalogImportCodec.Json), JsonSerializer.Serialize(await creator.LastConfiguration(owner), CatalogImportCodec.Json));
        }
    }
}
