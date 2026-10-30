using System.Net;
using System.Text.Json;

namespace Elifoot.Infrastructure.Catalog.Providers;

public enum FootballProviderKind { ApiFootball, Sportmonks, FootballData, Wikidata }
public sealed class NativeProviderOptions
{
    public FootballProviderKind Kind { get; set; }
    public string CredentialEnvironmentVariable { get; set; } = "";
    public int MinimumIntervalMilliseconds { get; set; } = 1100;
    public int MaxPages { get; set; } = 32;
    public int MaxRequestsPerFetch { get; set; } = 256;
    public int MaxFetchSeconds { get; set; } = 90;
    public bool FixtureData { get; set; }
    public Elifoot.Contracts.CatalogUsageRights? Rights { get; set; }
    public List<ProviderCompetition> Competitions { get; set; } = [];
}
public sealed class ProviderCompetition
{
    public string CountryCode { get; set; } = "";
    public string CountryName { get; set; } = "";
    public Elifoot.Contracts.FootballGender Gender { get; set; }
    public string Season { get; set; } = "";
    public string LeagueId { get; set; } = "";
    public string LeagueName { get; set; } = "";
    public string ProviderSeasonId { get; set; } = "";
    public string ProviderCountryId { get; set; } = "";
    public string ProviderCountryName { get; set; } = "";
    public string ReviewEvidence { get; set; } = "";
    public DateOnly? AsOf { get; set; }
    public List<WikidataTeam> WikidataTeams { get; set; } = [];
}
public record WikidataTeam(string Id, string ExpectedName, string CountryEntityId, string CategoryEntityId);
public sealed class ProviderRequestBudget(int maximum)
{
    private int _requests;
    public void Take() { if (Interlocked.Increment(ref _requests) > maximum) throw new ArgumentException("Limite de requisições da carga atingido."); }
}
public sealed class ProviderHttp(HttpClient client, FootballProviderKind kind, string? credential, int minimumIntervalMilliseconds = 1100)
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Dictionary<string, (JsonElement Data, DateTime Expires)> _cache = new();
    private DateTime _lastRequest, _blockedUntil;
    private HttpStatusCode _blockedStatus = HttpStatusCode.TooManyRequests;
    public async Task<JsonElement> Get(string relative, ProviderRequestBudget budget, CancellationToken ct)
    {
        if (Uri.TryCreate(relative, UriKind.Absolute, out _) || relative.StartsWith("/")) throw new ArgumentException("Caminho externo inválido.");
        if (kind != FootballProviderKind.Wikidata && string.IsNullOrWhiteSpace(credential)) throw new HttpRequestException("Credencial não configurada.", null, HttpStatusCode.Unauthorized);
        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(relative, out var cached) && cached.Expires > DateTime.UtcNow) return cached.Data.Clone();
            if (_blockedUntil > DateTime.UtcNow) throw new HttpRequestException("Fornecedor em espera temporária.", null, _blockedStatus);
            budget.Take();
            var delay = _lastRequest.AddMilliseconds(Math.Max(0, minimumIntervalMilliseconds)) - DateTime.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, relative);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("ElifootCatalog013B/1.0 (local-development)");
            if (kind == FootballProviderKind.ApiFootball) request.Headers.Add("x-apisports-key", credential);
            if (kind == FootballProviderKind.FootballData) request.Headers.Add("X-Auth-Token", credential);
            if (kind == FootballProviderKind.Sportmonks) request.Headers.TryAddWithoutValidation("Authorization", credential);
            _lastRequest = DateTime.UtcNow;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(15);
                _blockedStatus = HttpStatusCode.TooManyRequests;
                _blockedUntil = DateTime.UtcNow.AddSeconds(Math.Clamp(wait.TotalSeconds, 1, 86400));
            }
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Fornecedor {kind}: HTTP {(int)response.StatusCode}.", null, response.StatusCode);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var data = new MemoryStream(); byte[] buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, ct)) != 0)
            { if (data.Length + count > 20_000_000) throw new ArgumentException("Resposta do fornecedor excede 20 MB."); await data.WriteAsync(buffer.AsMemory(0, count), ct); }
            JsonElement root;
            try { using var json = JsonDocument.Parse(data.ToArray()); root = json.RootElement.Clone(); }
            catch (JsonException) { throw new ArgumentException("Fornecedor devolveu JSON inválido."); }
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind switch { JsonValueKind.Array => errors.GetArrayLength() > 0, JsonValueKind.Object => errors.EnumerateObject().Any(), _ => errors.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) })
                throw new ArgumentException("Fornecedor devolveu erros no payload HTTP 200.");
            if (kind == FootballProviderKind.Wikidata && root.Child("error").Text("code") == "maxlag")
            { _blockedUntil = DateTime.UtcNow.AddSeconds(30); _blockedStatus = HttpStatusCode.ServiceUnavailable; throw new HttpRequestException("Wikidata maxlag: aguardar e utilizar cache.", null, HttpStatusCode.ServiceUnavailable); }
            if (response.Headers.TryGetValues("x-ratelimit-requests-remaining", out var daily) && daily.FirstOrDefault() == "0")
            { _blockedUntil = DateTime.UtcNow.AddDays(1); _blockedStatus = HttpStatusCode.TooManyRequests; }
            else if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var minute) && minute.FirstOrDefault() == "0")
            { _blockedUntil = DateTime.UtcNow.AddMinutes(1); _blockedStatus = HttpStatusCode.TooManyRequests; }
            _cache[relative] = (root, DateTime.UtcNow.AddMinutes(2)); return root;
        }
        finally { _gate.Release(); }
    }
}
internal static class ProviderJson
{
    public static JsonElement Child(this JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) ? v : default;
    public static string Text(this JsonElement e) => e.ValueKind is JsonValueKind.String ? e.GetString()! : e.ValueKind == JsonValueKind.Number ? e.GetRawText() : "";
    public static string Text(this JsonElement e, string key) => e.Child(key).Text();
    public static int? Integer(this JsonElement e) => int.TryParse(e.Text(), out var n) ? n : null;
    public static double? Number(this JsonElement e) => double.TryParse(e.Text(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
    public static List<JsonElement> Rows(this JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().ToList() : [];
    public static string Required(this JsonElement e, string key) => !string.IsNullOrWhiteSpace(e.Text(key)) ? e.Text(key) : throw new ArgumentException($"Fornecedor sem campo obrigatório: {key}.");
}
