using Elifoot.Contracts;

namespace Elifoot.Infrastructure.Catalog.Providers;

public static class NativeProviderFactory
{
    public static List<ICatalogSource> Create(NativeProviderOptions options, HttpClient? httpClient = null, HttpClient? queryClient = null)
    {
        string? token = string.IsNullOrWhiteSpace(options.CredentialEnvironmentVariable) ? null : Environment.GetEnvironmentVariable(options.CredentialEnvironmentVariable);
        var baseUrl = options.Kind switch { FootballProviderKind.ApiFootball => "https://v3.football.api-sports.io/",
            FootballProviderKind.Sportmonks => "https://api.sportmonks.com/v3/football/", FootballProviderKind.FootballData => "https://api.football-data.org/v4/", _ => "https://www.wikidata.org/" };
        var transport = new ProviderHttp(httpClient ?? new HttpClient { BaseAddress = new(baseUrl), Timeout = Timeout.InfiniteTimeSpan }, options.Kind, token, options.MinimumIntervalMilliseconds);
        var query = options.Kind == FootballProviderKind.Wikidata ? new ProviderHttp(queryClient ?? new HttpClient { BaseAddress = new("https://query.wikidata.org/"), Timeout = Timeout.InfiniteTimeSpan }, options.Kind, null, options.MinimumIntervalMilliseconds) : null;
        var sources = new List<ICatalogSource>();
        foreach (var competition in options.Competitions)
        {
            foreach (var type in new[] { CatalogDataType.Clubs, CatalogDataType.Squads })
                sources.Add(options.Kind == FootballProviderKind.Wikidata ? new WikidataCatalogSource(transport, query!, competition, type, options.FixtureData)
                    : new NativeFootballCatalogSource(transport, options, competition, type));
            if (options.Kind is FootballProviderKind.ApiFootball or FootballProviderKind.FootballData)
                sources.Add(new NativeFootballCatalogSource(transport, options, competition, CatalogDataType.Rankings));
        }
        return sources;
    }
}
