using Elifoot.Domain;

int totalClubs = 128;
int seed = 199801;
string outputPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "seed-128-clubs.json");

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--clubs" && i + 1 < args.Length && int.TryParse(args[i + 1], out int c))
        totalClubs = c;
    else if (args[i] == "--seed" && i + 1 < args.Length && int.TryParse(args[i + 1], out int s))
        seed = s;
    else if (args[i] == "--output" && i + 1 < args.Length)
        outputPath = args[i + 1];
}

var world = WorldSeedFactory.CreateSeededWorld(
    worldName: $"Campeonato Brasileiro Clássico ({totalClubs} Clubes)",
    seed: seed,
    totalClubs: totalClubs);

var pkg = WorldEditorAndPackageService.ExportWorldToPackage(world, $"Elifoot98-Seed-{totalClubs}");
var validation = WorldEditorAndPackageService.ValidatePackage(pkg);

if (!validation.IsValid)
{
    Console.Error.WriteLine("ERRO DE VALIDAÇÃO DA SEED:");
    foreach (var err in validation.Errors)
    {
        Console.Error.WriteLine($" - {err}");
    }
    return 1;
}

string fullPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
string json = WorldEditorAndPackageService.SerializePackageToJson(pkg);
await File.WriteAllTextAsync(fullPath, json);

string csvPath = Path.ChangeExtension(fullPath, ".players.csv");
await File.WriteAllTextAsync(csvPath, WorldEditorAndPackageService.ExportPlayersToCsv(world));

Console.WriteLine($"Seed gerada e validada com sucesso: {fullPath}");
Console.WriteLine($"Clubes: {validation.ClubCount} (Divisões: 32, Distritais: {validation.ClubCount - 32})");
Console.WriteLine($"Jogadores: {validation.PlayerCount} | Técnicos IA: {validation.ManagerCount}");
return 0;
