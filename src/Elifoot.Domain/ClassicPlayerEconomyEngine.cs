using System.Text;
using Elifoot.Contracts;

namespace Elifoot.Domain;

public enum ClassicalNationalityClass
{
    SameCountry = 0,
    EquivalentCountry = 1,
    Foreigner = 2
}

public sealed record ClassicalCurrencySpec(
    int Index,
    string Name,
    string Suffix,
    int MinVoluntaryAuctionBid,
    int Factor2D83,
    int CurrencyFactorC,
    double TicketPriceStep);

public sealed record ClassicalPersonalAttributes(
    int ByteSumH,
    int NameLength,
    PlayerBehavior BehaviorA,
    int ModifierB,
    int ModifierD,
    bool IsStar);

public sealed record ClassicalPlayerEconomicBreakdown(
    Guid PlayerId,
    int Strength,
    decimal CurrentSalaryPerRound,
    decimal CalculatedBaseSalary,
    decimal IndividualMarketValue,
    decimal CandidateSigningPrice,
    decimal? FinalNegotiatedPrice,
    decimal MinimumAuctionBid,
    decimal RequiredRenewalSalary,
    decimal AiHiringInterestFactor,
    double InflationIndex,
    int InflationDisplayIndex,
    ClassicalNationalityClass NationalityClass,
    int BuyerForeignerCount,
    int BehaviorCodeA,
    int PersonalModifierB,
    int PersonalModifierD,
    bool IsStar);

public sealed record InflationHistoryEntry(
    int SeasonNumber,
    int EconomyYearCounter,
    double InflationBefore,
    double InflationAfter,
    int DisplayBefore,
    int DisplayAfter,
    double Delta,
    double SampleU1,
    double SampleU2,
    double SampleU3);

/// <summary>
/// Centralized backend domain engine reproducing the verified classical economy routines of ELIFOOT.EXE:
/// - SEG03:7FF4..8153: Base salary S = 50 * C * Round((35 * I * sqrt(R) * F^1.04 * 1000^0.04) / 50), clamped to [50*C, 50000*C], optional floor Max(S, S_curr).
/// - SEG09:371A..37E3: Market value V = Round(2000 * C * I * F^1.04 * (1 - 0.04*A - 0.02*B + 0.02*D)).
/// - SEG03:7516..766C & SEG14:07A4: Name transformation (H = sum of CP1252/Latin1 bytes) and modifiers A, B, D, IsStar.
/// - SEG12:0A2F..0AEA & SEG12:0F1C..0F7A: Nationality classification (0 = Same, 1 = Equivalent PLOP/Bosman, 2 = Foreigner).
/// - SEG03:63FA..6640: Candidate signing price Q with nationality stage ((Q+30000)*1.60, (Q+15000)*1.30, Q) and step quantization.
/// - SEG03:5BB1..5DAE: AI hiring interest factor (1.50 + 0.05*N, 1.15 + 0.05*N, 1.00) and asterisk multiplier (* 1.10).
/// - SEG11:3680, SEG06:2F8F, SEG05:0D3C..0E23: Internal inflation index I (init 2.0, bounds [0.5, 10.0], display Round(I*10)).
/// </summary>
public static class ClassicPlayerEconomyEngine
{
    public const string CalibratedMultiplayerPolicy = "MULTIPLAYER_CALIBRATED_012D";
    public const string ClassicalElifoot98Policy = "CLASSICAL_ELIFOOT98_V3_02C";

    public const double InitialInflationIndex = 2.0;
    public const double MinInflationIndex = 0.5;
    public const double MaxInflationIndex = 10.0;
    public const int InitialEconomyYearCounter = 1998;

    public const int DefaultCurrencyFactorC = 1;
    public const int OriginalReaisCurrencyFactorC = 5;
    public const double DefaultClubFactorR = 1.0;

    private static readonly double Pow1000_004 = Math.Pow(1000.0, 0.04);

    // SEG26:0062..025A currency table in ELIFOOT.EXE (copied to DS:2D40..2DA4 by SEG05:2719)
    public static readonly IReadOnlyList<ClassicalCurrencySpec> OriginalCurrencyTable =
    [
        new(0, "Escudos (contos)", " contos", 100_000, 1, 1, 0.5),
        new(1, "Euros", " euros", 500_000, 4, 4, 2.0),
        new(2, "Dólares", " dólares", 500_000, 4, 5, 2.0),
        new(3, "Reais", " reais", 500_000, 4, 5, 2.0),
        new(4, "Libras", " libras", 250_000, 4, 4, 2.0)
    ];

    private static readonly HashSet<string> PlopCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "BR", "PT", "AO", "MZ", "CV", "GW", "ST"
    };

    private static readonly HashSet<string> BosmanEuCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "PT", "ES", "FR", "IT", "DE", "GB", "NL", "BE", "AT", "DK", "FI", "GR", "IE", "LU", "SE"
    };

    public static ClassicPlayerEconomyRules ClassicalRules(int currencyFactorC = DefaultCurrencyFactorC) => new()
    {
        Policy = ClassicalElifoot98Policy,
        CurrencyFactorC = Math.Max(1, currencyFactorC),
        UseClassicalSalaryFormula = true,
        UseClassicalMarketValuation = true,
        UseClassicalNationalityPricing = true,
        UseClassicalAiHiringInterest = true,
        UseSeasonalInflationEvolution = true
    };

    public static void EnableClassicalEconomy(
        GameWorld world,
        int currencyFactorC = DefaultCurrencyFactorC,
        bool recalculateInitialPlayerValues = false)
    {
        world.Economy.PlayerEconomyRules = ClassicalRules(currencyFactorC);
        if (world.Economy.InflationIndex <= 0)
            world.Economy.InflationIndex = InitialInflationIndex;
        world.Economy.InflationIndex = Math.Clamp(world.Economy.InflationIndex, MinInflationIndex, MaxInflationIndex);
        if (world.Economy.EconomyYearCounter <= 0)
            world.Economy.EconomyYearCounter = InitialEconomyYearCounter + Math.Max(0, world.SeasonNumber - 1);

        if (!recalculateInitialPlayerValues) return;

        var clubById = world.Clubs.ToDictionary(c => c.Id);
        foreach (var player in world.Players)
        {
            var attrs = DerivePersonalAttributesFromName(player.Name, player.Position);
            player.PersonalModifierB ??= attrs.ModifierB;
            player.PersonalModifierD ??= attrs.ModifierD;
            var club = player.ClubId.HasValue ? clubById.GetValueOrDefault(player.ClubId.Value) : null;
            player.SalaryPerRound = CalculateReferenceSalaryPerRound(world, player, club);
            player.MarketPrice = CalculateMarketValue(world, player);
            if (player.IsListedForTransfer && player.AskingPrice.HasValue)
                player.AskingPrice = Math.Round(player.MarketPrice * 0.95m, 0);
        }
    }

    public static bool IsClassicalSalaryEnabled(GameWorld? world) =>
        world?.Economy.PlayerEconomyRules.UseClassicalSalaryFormula == true
        || string.Equals(world?.Economy.PlayerEconomyRules.Policy, ClassicalElifoot98Policy, StringComparison.Ordinal);

    public static bool IsClassicalMarketValuationEnabled(GameWorld? world) =>
        world?.Economy.PlayerEconomyRules.UseClassicalMarketValuation == true
        || string.Equals(world?.Economy.PlayerEconomyRules.Policy, ClassicalElifoot98Policy, StringComparison.Ordinal);

    public static bool IsClassicalNationalityPricingEnabled(GameWorld? world) =>
        world?.Economy.PlayerEconomyRules.UseClassicalNationalityPricing == true
        || string.Equals(world?.Economy.PlayerEconomyRules.Policy, ClassicalElifoot98Policy, StringComparison.Ordinal);

    public static bool IsClassicalAiHiringInterestEnabled(GameWorld? world) =>
        world?.Economy.PlayerEconomyRules.UseClassicalAiHiringInterest == true
        || string.Equals(world?.Economy.PlayerEconomyRules.Policy, ClassicalElifoot98Policy, StringComparison.Ordinal);

    /// <summary>
    /// Reproduces SEG03:7FF4..8153:
    /// S = 50 * C * Round((35 * I * sqrt(R) * F^1.04 * 1000^0.04) / 50)
    /// Clamped to [50 * C, 50000 * C], with optional floor Max(S, currentSalaryFloor).
    /// </summary>
    public static decimal CalculateClassicalBaseSalary(
        int strength,
        double inflationIndex = InitialInflationIndex,
        double clubFactorR = DefaultClubFactorR,
        int currencyFactorC = DefaultCurrencyFactorC,
        decimal? currentSalaryFloor = null)
    {
        int f = Math.Clamp(strength, 1, 50);
        double i = Math.Clamp(inflationIndex, MinInflationIndex, MaxInflationIndex);
        double r = Math.Max(0.01, clubFactorR);
        int c = Math.Max(1, currencyFactorC);

        double rawUnits = (35.0 * i * Math.Sqrt(r) * Math.Pow(f, 1.04) * Pow1000_004) / 50.0;
        long roundedUnits = (long)Math.Round(rawUnits, MidpointRounding.ToEven);
        decimal salary = 50m * c * roundedUnits;

        decimal minSalary = 50m * c;
        decimal maxSalary = 50_000m * c;
        salary = Math.Clamp(salary, minSalary, maxSalary);

        if (currentSalaryFloor.HasValue && currentSalaryFloor.Value > 0m)
            salary = Math.Max(salary, currentSalaryFloor.Value);

        return salary;
    }

    public static double GetClubFactorR(GameWorld? world, Club? club)
    {
        if (world != null && club != null && world.Economy.ClubFactorsR.TryGetValue(club.Id, out double r) && r > 0)
            return r;
        return DefaultClubFactorR;
    }

    public static void SetClubFactorR(GameWorld world, Guid clubId, double factorR)
    {
        world.Economy.ClubFactorsR[clubId] = Math.Max(0.01, factorR);
    }

    public static decimal CalculateReferenceSalaryPerRound(GameWorld? world, Player player, Club? club = null)
    {
        if (IsClassicalSalaryEnabled(world))
        {
            club ??= player.ClubId.HasValue ? world?.Clubs.FirstOrDefault(c => c.Id == player.ClubId.Value) : null;
            double inflation = world?.Economy.InflationIndex ?? InitialInflationIndex;
            double r = GetClubFactorR(world, club);
            int c = Math.Max(1, world?.Economy.PlayerEconomyRules.CurrencyFactorC ?? DefaultCurrencyFactorC);
            return CalculateClassicalBaseSalary(player.Strength, inflation, r, c);
        }

        return MarketAndEconomyEngine.CalculateFairSalaryPerRound(player.Strength, player.IsStar, player.Position);
    }

    /// <summary>
    /// Reproduces SEG09:371A..37E3:
    /// V = Round(2000 * C * I * F^1.04 * (1 - 0.04*A - 0.02*B + 0.02*D))
    /// </summary>
    public static decimal CalculateClassicalMarketValue(
        int strength,
        PlayerBehavior behavior,
        int modifierB,
        int modifierD,
        double inflationIndex = InitialInflationIndex,
        int currencyFactorC = DefaultCurrencyFactorC)
    {
        int f = Math.Clamp(strength, 1, 50);
        int a = Math.Clamp((int)behavior, 0, 5);
        int b = Math.Clamp(modifierB, 0, 10);
        int d = Math.Clamp(modifierD, 1, 10);
        double i = Math.Clamp(inflationIndex, MinInflationIndex, MaxInflationIndex);
        int c = Math.Max(1, currencyFactorC);

        double individualFactor = 1.0 - (0.04 * a) - (0.02 * b) + (0.02 * d);
        double rawValue = 2000.0 * c * i * Math.Pow(f, 1.04) * individualFactor;
        long rounded = (long)Math.Round(rawValue, MidpointRounding.ToEven);
        return Math.Max(0m, rounded);
    }

    public static decimal CalculateClassicalMarketValue(
        Player player,
        double inflationIndex = InitialInflationIndex,
        int currencyFactorC = DefaultCurrencyFactorC)
    {
        var (a, b, d) = ResolveIndividualModifiers(player);
        return CalculateClassicalMarketValue(player.Strength, (PlayerBehavior)a, b, d, inflationIndex, currencyFactorC);
    }

    public static decimal CalculateMarketValue(GameWorld? world, Player player)
    {
        if (IsClassicalMarketValuationEnabled(world))
        {
            double inflation = world?.Economy.InflationIndex ?? InitialInflationIndex;
            int c = Math.Max(1, world?.Economy.PlayerEconomyRules.CurrencyFactorC ?? DefaultCurrencyFactorC);
            return CalculateClassicalMarketValue(player, inflation, c);
        }

        return MarketAndEconomyEngine.CalculateCalibratedFairValue(player);
    }

    /// <summary>
    /// Reproduces SEG14:07A4 in-place .EFT string de-obfuscation:
    /// s[i] = (s[i] - s[i-1]) mod 256 for i = L down to 1, where s[0] = L.
    /// </summary>
    public static string DeobfuscateEftName(ReadOnlySpan<byte> obfuscatedBytes)
    {
        if (obfuscatedBytes.IsEmpty) return string.Empty;
        int len = Math.Min(20, Math.Min((int)obfuscatedBytes[0], obfuscatedBytes.Length - 1));
        if (len <= 0) return string.Empty;

        Span<byte> work = stackalloc byte[len + 1];
        obfuscatedBytes[..(len + 1)].CopyTo(work);
        for (int i = len; i >= 1; i--)
            work[i] = unchecked((byte)(work[i] - work[i - 1]));

        return Encoding.Latin1.GetString(work.Slice(1, len));
    }

    /// <summary>
    /// Inverse of SEG14:07A4 for encoding a plain name into the .EFT obfuscated byte representation.
    /// </summary>
    public static byte[] ObfuscateEftName(string? plainName)
    {
        string clean = NormalizeNameForHash(plainName);
        byte[] plainBytes = Encoding.Latin1.GetBytes(clean);
        int len = Math.Min(20, plainBytes.Length);
        byte[] result = new byte[len + 1];
        result[0] = (byte)len;
        for (int i = 1; i <= len; i++)
            result[i] = unchecked((byte)(plainBytes[i - 1] + result[i - 1]));
        return result;
    }

    /// <summary>
    /// Reproduces SEG03:7516..766C:
    /// H = sum_{i=1..L} byte(S[i]) over the de-obfuscated plain name without '*'.
    /// A_raw = 5 - trunc(sqrt(H % (6 * L))), shifted by (A_raw + 2) % 6 for Defenders (Position == D).
    /// D = (H % 10) + 1 (1..10).
    /// IsStar = D >= 8 and Position in {M, A}.
    /// B = H % 11 (0..10).
    /// </summary>
    public static ClassicalPersonalAttributes DerivePersonalAttributesFromName(string? name, PlayerPosition position)
    {
        string clean = NormalizeNameForHash(name);
        if (clean.Length == 0) clean = " ";
        byte[] bytes = Encoding.Latin1.GetBytes(clean);
        int len = bytes.Length;

        int h = 0;
        for (int i = 0; i < len; i++)
            h += bytes[i];

        int modA = h % (6 * len);
        int aRaw = 5 - (int)Math.Truncate(Math.Sqrt(modA));
        aRaw = Math.Clamp(aRaw, 0, 5);
        int codeA = position == PlayerPosition.D ? (aRaw + 2) % 6 : aRaw;

        int d = (h % 10) + 1;
        bool isStar = d >= 8 && (position == PlayerPosition.M || position == PlayerPosition.A);
        int b = h % 11;

        return new ClassicalPersonalAttributes(h, len, (PlayerBehavior)codeA, b, d, isStar);
    }

    public static (int A, int B, int D) ResolveIndividualModifiers(Player player)
    {
        int a = Math.Clamp((int)player.Behavior, 0, 5);
        if (player.PersonalModifierB.HasValue && player.PersonalModifierD.HasValue)
            return (a, Math.Clamp(player.PersonalModifierB.Value, 0, 10), Math.Clamp(player.PersonalModifierD.Value, 1, 10));

        var derived = DerivePersonalAttributesFromName(player.Name, player.Position);
        int b = Math.Clamp(player.PersonalModifierB ?? derived.ModifierB, 0, 10);
        int d = Math.Clamp(player.PersonalModifierD ?? derived.ModifierD, 1, 10);
        return (a, b, d);
    }

    private static string NormalizeNameForHash(string? name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        string trimmed = name.EndsWith('*') ? name[..^1] : name;
        return trimmed.Length > 20 ? trimmed[..20] : trimmed;
    }

    public static string NormalizeCountryCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return "BR";
        string token = country.Trim().ToUpperInvariant();
        return token switch
        {
            "BR" or "BRA" or "BRASIL" or "BRAZIL" => "BR",
            "PT" or "PRT" or "POR" or "PORTUGAL" => "PT",
            "AO" or "ANG" or "ANGOLA" => "AO",
            "MZ" or "MOZ" or "MOCAMBIQUE" or "MOÇAMBIQUE" or "MOZAMBIQUE" => "MZ",
            "CV" or "CPV" or "CABO VERDE" or "CAPE VERDE" => "CV",
            "GW" or "GNB" or "GUINE-BISSAU" or "GUINÉ-BISSAU" => "GW",
            "ST" or "STP" or "SAO TOME E PRINCIPE" or "SÃO TOMÉ E PRÍNCIPE" => "ST",
            "AR" or "ARG" or "ARGENTINA" => "AR",
            "UY" or "URU" or "URUGUAI" or "URUGUAY" => "UY",
            "PY" or "PAR" or "PARAGUAI" or "PARAGUAY" => "PY",
            "CO" or "COL" or "COLOMBIA" or "COLÔMBIA" => "CO",
            "CL" or "CHI" or "CHILE" => "CL",
            "GB" or "ENG" or "UK" or "GBR" or "INGLATERRA" or "ENGLAND" or "REINO UNIDO" => "GB",
            "ES" or "ESP" or "ESPANHA" or "SPAIN" => "ES",
            "DE" or "GER" or "ALE" or "ALEMANHA" or "GERMANY" => "DE",
            "IT" or "ITA" or "ITALIA" or "ITÁLIA" or "ITALY" => "IT",
            "FR" or "FRA" or "FRANCA" or "FRANÇA" or "FRANCE" => "FR",
            "NL" or "NED" or "NLD" or "HOLANDA" or "PAISES BAIXOS" or "PAÍSES BAIXOS" or "NETHERLANDS" => "NL",
            "BE" or "BEL" or "BELGICA" or "BÉLGICA" or "BELGIUM" => "BE",
            "AT" or "AUT" or "AUSTRIA" or "ÁUSTRIA" => "AT",
            "DK" or "DEN" or "DINAMARCA" or "DENMARK" => "DK",
            "FI" or "FIN" or "FINLANDIA" or "FINLÂNDIA" => "FI",
            "GR" or "GRE" or "GRECIA" or "GRÉCIA" or "GREECE" => "GR",
            "IE" or "IRL" or "IRLANDA" or "IRELAND" => "IE",
            "LU" or "LUX" or "LUXEMBURGO" or "LUXEMBOURG" => "LU",
            "SE" or "SWE" or "SUECIA" or "SUÉCIA" or "SWEDEN" => "SE",
            _ => token
        };
    }

    public static string ResolveClubCountry(GameWorld? world, Club? club)
    {
        if (club != null && !string.IsNullOrWhiteSpace(club.Country))
            return NormalizeCountryCode(club.Country);
        return "BR";
    }

    public static string ResolvePlayerCountry(GameWorld? world, Player player, Club? fallbackClub = null)
    {
        if (!string.IsNullOrWhiteSpace(player.Nationality))
            return NormalizeCountryCode(player.Nationality);
        var club = fallbackClub ?? (player.ClubId.HasValue ? world?.Clubs.FirstOrDefault(c => c.Id == player.ClubId.Value) : null);
        return ResolveClubCountry(world, club);
    }

    /// <summary>
    /// Reproduces SEG12:0A2F..0AEA:
    /// 0 = SameCountry (playerCountry == buyerClubCountry)
    /// 1 = EquivalentCountry (both in PLOP/Lusophone or both in Bosman/EU)
    /// 2 = Foreigner
    /// </summary>
    public static ClassicalNationalityClass ClassifyNationality(string? playerCountry, string? buyerClubCountry)
    {
        string p = NormalizeCountryCode(playerCountry);
        string b = NormalizeCountryCode(buyerClubCountry);

        if (string.Equals(p, b, StringComparison.OrdinalIgnoreCase))
            return ClassicalNationalityClass.SameCountry;

        if ((PlopCountries.Contains(p) && PlopCountries.Contains(b))
            || (BosmanEuCountries.Contains(p) && BosmanEuCountries.Contains(b)))
            return ClassicalNationalityClass.EquivalentCountry;

        return ClassicalNationalityClass.Foreigner;
    }

    /// <summary>
    /// Reproduces the nationality price stage in SEG03:63FA (6501..6555):
    /// - SameCountry (0): Round((Q + 30000) * 1.60)
    /// - EquivalentCountry (1): Round((Q + 15000) * 1.30)
    /// - Foreigner (2): Round(Q)
    /// </summary>
    public static decimal ApplyClassicalNationalityPriceStage(
        decimal baseQuoteQ,
        ClassicalNationalityClass nationalityClass)
    {
        decimal q = Math.Max(0m, baseQuoteQ);
        return nationalityClass switch
        {
            ClassicalNationalityClass.SameCountry => Math.Round((q + 30_000m) * 1.60m, 0, MidpointRounding.ToEven),
            ClassicalNationalityClass.EquivalentCountry => Math.Round((q + 15_000m) * 1.30m, 0, MidpointRounding.ToEven),
            _ => Math.Round(q, 0, MidpointRounding.ToEven)
        };
    }

    /// <summary>
    /// Reproduces the magnitude quantization ladder in SEG03:6558..6640:
    /// >= 2,000,000 -> step 100,000
    /// >= 1,000,000 -> step 50,000
    /// >    500,000 -> step 25,000
    /// >    100,000 -> step 10,000
    /// <=   100,000 -> step 1,000
    /// </summary>
    public static decimal QuantizeClassicalPrice(decimal price)
    {
        if (price <= 0m) return 0m;
        decimal step = price >= 2_000_000m ? 100_000m
            : price >= 1_000_000m ? 50_000m
            : price > 500_000m ? 25_000m
            : price > 100_000m ? 10_000m
            : 1_000m;

        decimal quantized = step * Math.Round(price / step, 0, MidpointRounding.ToEven);
        return Math.Max(1_000m, quantized);
    }

    public static decimal CalculateCandidateSigningPrice(
        GameWorld? world,
        Player player,
        Club? buyerClub,
        Club? sellerClub = null,
        bool quantize = false,
        decimal? baseQuoteOverride = null)
    {
        sellerClub ??= player.ClubId.HasValue ? world?.Clubs.FirstOrDefault(c => c.Id == player.ClubId.Value) : null;
        decimal baseQuote = baseQuoteOverride ?? CalculateMarketValue(world, player);

        if (IsClassicalNationalityPricingEnabled(world) || baseQuoteOverride.HasValue)
        {
            string pCountry = ResolvePlayerCountry(world, player, sellerClub);
            string bCountry = ResolveClubCountry(world, buyerClub);
            var natClass = ClassifyNationality(pCountry, bCountry);
            decimal candidate = ApplyClassicalNationalityPriceStage(baseQuote, natClass);
            return quantize ? QuantizeClassicalPrice(candidate) : candidate;
        }

        if (player.IsListedForTransfer && player.AskingPrice.HasValue)
            return player.AskingPrice.Value;

        return Math.Round(baseQuote * 1.12m, 0);
    }

    public static int CountForeignersInSquad(GameWorld? world, Club? club)
    {
        if (world == null || club == null) return 0;
        string clubCountry = ResolveClubCountry(world, club);
        int count = 0;
        foreach (var p in world.Players)
        {
            if (p.ClubId != club.Id) continue;
            string pCountry = ResolvePlayerCountry(world, p, club);
            if (ClassifyNationality(pCountry, clubCountry) == ClassicalNationalityClass.Foreigner)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Reproduces SEG03:5BB1..5DAE AI hiring interest routine:
    /// - SameCountry (0): 1.50 + 0.05 * N
    /// - EquivalentCountry (1): 1.15 + 0.05 * N
    /// - Foreigner (2): 1.00
    /// Multiplied by 1.10 if IsStar == true (SEG03:5D8F..5DAE).
    /// </summary>
    public static decimal CalculateClassicalAiHiringInterestFactor(
        ClassicalNationalityClass nationalityClass,
        int buyerForeignerCount,
        bool isStar)
    {
        int n = Math.Max(0, buyerForeignerCount);
        decimal factor = nationalityClass switch
        {
            ClassicalNationalityClass.SameCountry => 1.50m + (0.05m * n),
            ClassicalNationalityClass.EquivalentCountry => 1.15m + (0.05m * n),
            _ => 1.00m
        };

        if (isStar)
            factor *= 1.10m;

        return Math.Round(factor, 4);
    }

    public static decimal CalculateAiHiringInterestFactor(GameWorld? world, Player player, Club? buyerClub, Club? sellerClub = null)
    {
        sellerClub ??= player.ClubId.HasValue ? world?.Clubs.FirstOrDefault(c => c.Id == player.ClubId.Value) : null;
        string pCountry = ResolvePlayerCountry(world, player, sellerClub);
        string bCountry = ResolveClubCountry(world, buyerClub);
        var natClass = ClassifyNationality(pCountry, bCountry);
        int foreigners = CountForeignersInSquad(world, buyerClub);
        return CalculateClassicalAiHiringInterestFactor(natClass, foreigners, player.IsStar);
    }

    /// <summary>
    /// Reproduces SEG11:3680 & SEG06:2F8F inflation display: Round(I * 10).
    /// </summary>
    public static int CalculateInflationDisplay(double inflationIndex) =>
        (int)Math.Round(Math.Clamp(inflationIndex, MinInflationIndex, MaxInflationIndex) * 10.0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Reproduces SEG05:0D3C..0E23 seasonal inflation update:
    /// If Y % 100 &lt; 50: delta = 0.01 * (U1 + U2 - U3)
    /// Else: delta = 0.01 * (U1 - U2 - U3)
    /// I = Clamp(I + delta, 0.5, 10.0)
    /// </summary>
    public static (double NextInflation, double Delta) AdvanceInflationIndex(
        double currentInflationIndex,
        int economyYearCounterY,
        double sampleU1,
        double sampleU2,
        double sampleU3)
    {
        double clampedCurrent = Math.Clamp(currentInflationIndex, MinInflationIndex, MaxInflationIndex);
        double u1 = Math.Clamp(sampleU1, 0.0, 1.0);
        double u2 = Math.Clamp(sampleU2, 0.0, 1.0);
        double u3 = Math.Clamp(sampleU3, 0.0, 1.0);

        int yearMod100 = ((economyYearCounterY % 100) + 100) % 100;
        double delta = yearMod100 < 50
            ? 0.01 * (u1 + u2 - u3)
            : 0.01 * (u1 - u2 - u3);

        double next = Math.Clamp(clampedCurrent + delta, MinInflationIndex, MaxInflationIndex);
        return (next, delta);
    }

    public static InflationHistoryEntry AdvanceWorldInflationAtSeasonEnd(GameWorld world, int completedSeason)
    {
        if (world.Economy.InflationIndex <= 0)
            world.Economy.InflationIndex = InitialInflationIndex;
        if (world.Economy.EconomyYearCounter <= 0)
            world.Economy.EconomyYearCounter = InitialEconomyYearCounter + Math.Max(0, completedSeason - 1);

        var existing = world.Economy.InflationHistory.FirstOrDefault(h => h.SeasonNumber == completedSeason);
        if (existing != null) return existing;

        ulong rngSeed = unchecked(
            ((ulong)(uint)world.Seed * 6364136223846793005UL)
            + ((ulong)(uint)completedSeason * 1442695040888963407UL)
            + 0x02C0_1998UL);
        var rng = new DeterministicRng(unchecked((long)rngSeed));
        double u1 = rng.NextDouble();
        double u2 = rng.NextDouble();
        double u3 = rng.NextDouble();

        int yearY = world.Economy.EconomyYearCounter;
        double before = Math.Clamp(world.Economy.InflationIndex, MinInflationIndex, MaxInflationIndex);
        int displayBefore = CalculateInflationDisplay(before);

        var (after, delta) = world.Economy.PlayerEconomyRules.UseSeasonalInflationEvolution
            ? AdvanceInflationIndex(before, yearY, u1, u2, u3)
            : (before, 0.0);

        world.Economy.InflationIndex = after;
        world.Economy.EconomyYearCounter = yearY + 1;

        var entry = new InflationHistoryEntry(
            completedSeason,
            yearY,
            before,
            after,
            displayBefore,
            CalculateInflationDisplay(after),
            delta,
            u1,
            u2,
            u3);
        world.Economy.InflationHistory.Add(entry);

        if (IsClassicalMarketValuationEnabled(world))
        {
            foreach (var p in world.Players)
            {
                p.MarketPrice = CalculateMarketValue(world, p);
                if (p.IsListedForTransfer && p.AskingPrice.HasValue)
                    p.AskingPrice = Math.Round(p.MarketPrice * 0.95m, 0);
            }
        }

        return entry;
    }

    /// <summary>
    /// Computes all 10 mandatory economic values for a player in a single authoritative snapshot without mutating state.
    /// </summary>
    public static ClassicalPlayerEconomicBreakdown EvaluatePlayerEconomy(
        GameWorld? world,
        Player player,
        Club? buyerClub = null,
        Club? sellerClub = null,
        decimal? negotiatedPrice = null,
        AuctionItem? activeAuction = null)
    {
        sellerClub ??= player.ClubId.HasValue ? world?.Clubs.FirstOrDefault(c => c.Id == player.ClubId.Value) : null;
        double inflation = world?.Economy.InflationIndex ?? InitialInflationIndex;
        int c = Math.Max(1, world?.Economy.PlayerEconomyRules.CurrencyFactorC ?? DefaultCurrencyFactorC);
        double r = GetClubFactorR(world, buyerClub ?? sellerClub);

        decimal baseSalary = IsClassicalSalaryEnabled(world)
            ? CalculateClassicalBaseSalary(player.Strength, inflation, r, c)
            : CalculateReferenceSalaryPerRound(world, player, buyerClub ?? sellerClub);

        decimal marketValue = IsClassicalMarketValuationEnabled(world)
            ? CalculateClassicalMarketValue(player, inflation, c)
            : CalculateMarketValue(world, player);

        string pCountry = ResolvePlayerCountry(world, player, sellerClub);
        string bCountry = ResolveClubCountry(world, buyerClub ?? sellerClub);
        var natClass = ClassifyNationality(pCountry, bCountry);
        int foreigners = CountForeignersInSquad(world, buyerClub);

        decimal candidatePrice = ApplyClassicalNationalityPriceStage(marketValue, natClass);
        decimal aiInterest = CalculateClassicalAiHiringInterestFactor(natClass, foreigners, player.IsStar);

        decimal minAuctionBid = activeAuction != null
            ? activeAuction.MinimumBid
            : 500_000m;

        decimal requiredRenewalSalary = world != null && sellerClub != null
            ? ClassicEconomyEngine.PreviewRenewal(world, sellerClub, player).RequiredSalary
            : Math.Max(player.SalaryPerRound, baseSalary);

        var (modA, modB, modD) = ResolveIndividualModifiers(player);

        return new ClassicalPlayerEconomicBreakdown(
            PlayerId: player.Id,
            Strength: player.Strength,
            CurrentSalaryPerRound: player.SalaryPerRound,
            CalculatedBaseSalary: baseSalary,
            IndividualMarketValue: marketValue,
            CandidateSigningPrice: candidatePrice,
            FinalNegotiatedPrice: negotiatedPrice,
            MinimumAuctionBid: minAuctionBid,
            RequiredRenewalSalary: requiredRenewalSalary,
            AiHiringInterestFactor: aiInterest,
            InflationIndex: inflation,
            InflationDisplayIndex: CalculateInflationDisplay(inflation),
            NationalityClass: natClass,
            BuyerForeignerCount: foreigners,
            BehaviorCodeA: modA,
            PersonalModifierB: modB,
            PersonalModifierD: modD,
            IsStar: player.IsStar);
    }
}
