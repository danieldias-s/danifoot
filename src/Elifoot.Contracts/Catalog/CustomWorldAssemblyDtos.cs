namespace Elifoot.Contracts;

public record CreateWorldAssemblyRequest(string Name, FootballGender Gender, List<string> Countries, int Seed,
    Guid? SourcePreviewId = null, WorldPackageExportDto? Package = null, bool AllowPersonalizedContent = false);
public record UpdateWorldAssemblyRequest(string Name, List<string> Countries, WorldPackageExportDto Package,
    int ExpectedVersion, bool SavePreset = false, bool AllowPersonalizedContent = false);
public record WorldAssemblyDto(Guid Id, string Name, FootballGender Gender, List<string> Countries, int Seed,
    WorldPackageExportDto Package, int Version, bool IsSaved, bool AllowPersonalizedContent, PackageValidationResultDto Validation);
public record WorldAssemblySummaryDto(Guid Id, string Name, FootballGender Gender, List<string> Countries,
    int Seed, int Version, DateTime UpdatedAtUtc);
public record ConfirmWorldAssemblyRequest(string IdempotencyKey, string? WorldName = null, int MaxHumans = 8);
public record CustomAssemblyWorldOrigin(Guid AssemblyId, int AssemblyVersion, string PackageHash,
    List<string> Countries, DateTime ConfirmedAtUtc);

public record AssemblyCsvRequest(string Csv);
public record AssemblyPackageRequest(WorldPackageExportDto Package);
