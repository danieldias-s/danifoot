using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Infrastructure.Catalog.Import016A;

public sealed class ImportProviderException(string code) : Exception(code) {public string Code=>Message;}
public record ImportResponse016A(JsonElement Data, DateTime FetchedAt, bool FromCache);
public sealed class ImportHttp016A(IServiceScopeFactory scopes, Func<string,HttpClient> clients, Import016AOptions options)
{
    private static readonly ConcurrentDictionary<string,SemaphoreSlim> Gates=new();
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public bool HasApiKey=>!string.IsNullOrWhiteSpace(options.ApiFootballKey);
    public async Task<ImportProviderStatus016A> Status(string provider,CancellationToken ct=default) {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var key=provider+":"+DateTime.UtcNow.ToString("yyyy-MM-dd");
        var budget=await db.Set<Import016ABudget>().AsNoTracking().FirstOrDefaultAsync(x=>x.Key==key,ct);
        bool configured=provider!="api-football"||HasApiKey;
        return new(provider,configured,configured?"READY":"NOT_CONNECTED",provider=="api-football"?Math.Clamp(options.DailyRequestBudget,1,100):1000,
            budget?.Used??0,budget?.Remaining,budget?.BlockedUntil> DateTime.UtcNow?budget.BlockedUntil:null,
            await db.Set<Import016ACache>().CountAsync(x=>x.Provider==provider,ct));
    }
    public async Task<ImportResponse016A> Get(string provider,string resource,bool refresh,CancellationToken ct=default) {
        if(provider is not ("api-football" or "wikidata") || Uri.TryCreate(resource,UriKind.Absolute,out _) || resource.StartsWith('/') || resource.Contains(".."))throw new ArgumentException("Recurso de importação inválido.");
        if(provider=="api-football" && !HasApiKey)throw new ImportProviderException("NOT_CONNECTED");
        if(provider=="api-football" && options.ApiFootballRights is not {AccessAllowed:true})throw new ImportProviderException("ACCESS_RIGHTS_NOT_CONFIRMED");
        bool store=provider=="wikidata"||options.ApiFootballRights is {StorageAllowed:true,RetainUntil:null};
        string hash=Hash(provider+":"+resource);
        using var first=scopes.CreateScope();var firstDb=first.ServiceProvider.GetRequiredService<CatalogDbContext>();
        if(store&&!refresh && await firstDb.Set<Import016ACache>().AsNoTracking().FirstOrDefaultAsync(x=>x.Key==hash&&x.ExpiresAt>DateTime.UtcNow,ct) is {} cached)
            return new(JsonDocument.Parse(cached.Payload).RootElement.Clone(),DateTime.SpecifyKind(cached.FetchedAt,DateTimeKind.Utc),true);
        var gate=Gates.GetOrAdd(provider,_=>new(1,1));await gate.WaitAsync(ct);
        try {
            if(store&&!refresh && await firstDb.Set<Import016ACache>().AsNoTracking().FirstOrDefaultAsync(x=>x.Key==hash&&x.ExpiresAt>DateTime.UtcNow,ct) is {} shared)
                return new(JsonDocument.Parse(shared.Payload).RootElement.Clone(),DateTime.SpecifyKind(shared.FetchedAt,DateTimeKind.Utc),true);
            for(int attempt=0;attempt<=Math.Clamp(options.Retries,0,2);attempt++) {
                var reserved=await Reserve(provider,resource=="status",ct);
                var delay=reserved-DateTime.UtcNow;if(delay>TimeSpan.Zero)await Task.Delay(delay,ct);
                try {
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds,1,60)));
                    string name=provider=="api-football"?"016A.ApiFootball":resource.StartsWith("sparql?")?"016A.WikidataQuery":"016A.Wikidata";
                    using var request=new HttpRequestMessage(HttpMethod.Get,resource);
                    if(provider=="api-football")request.Headers.TryAddWithoutValidation("x-apisports-key",options.ApiFootballKey);
                    else {request.Headers.Accept.ParseAdd("application/json");request.Headers.UserAgent.ParseAdd("ElifootCatalog016A/1.0 (local-development)");}
                    using var response=await clients(name).SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
                    int? Header(string key)=>response.Headers.TryGetValues(key,out var values)&&int.TryParse(values.FirstOrDefault(),out int n)?n:null;
                    int? remaining=Header("x-ratelimit-requests-remaining"),limit=Header("x-ratelimit-requests-limit");
                    await UpdateBudget(provider,remaining,limit,Header("X-RateLimit-Remaining")==0?DateTime.UtcNow.AddMinutes(1):null,ct);
                    if(response.StatusCode==HttpStatusCode.TooManyRequests) {
                        var until=response.Headers.RetryAfter?.Date?.UtcDateTime??DateTime.UtcNow.Add(response.Headers.RetryAfter?.Delta??TimeSpan.FromMinutes(15));
                        await UpdateBudget(provider,remaining,limit,until,ct);await Audit(provider,resource,429,remaining,"RATE_LIMIT",0,ct);throw new ImportProviderException("RATE_LIMIT");
                    }
                    if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) {await Audit(provider,resource,(int)response.StatusCode,remaining,"AUTH_OR_PLAN_DENIED",0,ct);throw new ImportProviderException("AUTH_OR_PLAN_DENIED");}
                    if(!response.IsSuccessStatusCode) {
                        await Audit(provider,resource,(int)response.StatusCode,remaining,"HTTP_ERROR",0,ct);
                        if((int)response.StatusCode>=500&&attempt<options.Retries){await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2,attempt)),ct);continue;}
                        throw new ImportProviderException("PROVIDER_UNAVAILABLE");
                    }
                    if(response.Content.Headers.ContentLength>4_000_000)throw new ImportProviderException("RESPONSE_TOO_LARGE");
                    await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var buffer=new MemoryStream();byte[] chunk=new byte[8192];int count;
                    while((count=await stream.ReadAsync(chunk,timeout.Token))>0){if(buffer.Length+count>4_000_000)throw new ImportProviderException("RESPONSE_TOO_LARGE");await buffer.WriteAsync(chunk.AsMemory(0,count),timeout.Token);}
                    using var document=JsonDocument.Parse(buffer.ToArray());var data=document.RootElement;
                    if(data.TryGetProperty("error",out var error)&&error.ValueKind==JsonValueKind.Object) {
                        string code=error.TryGetProperty("code",out var c)?c.GetString()??"":"";
                        if(code=="maxlag"){await UpdateBudget(provider,null,null,DateTime.UtcNow.AddSeconds(30),ct);await Audit(provider,resource,200,remaining,"MAXLAG",0,ct);throw new ImportProviderException("MAXLAG");}
                        throw new ImportProviderException("PROVIDER_PAYLOAD_ERROR");
                    }
                    if(data.TryGetProperty("errors",out var errors)&&errors.ValueKind switch {JsonValueKind.Object=>errors.EnumerateObject().Any(),JsonValueKind.Array=>errors.GetArrayLength()>0,_=>false}) {
                        string safe=errors.GetRawText().Contains("limit",StringComparison.OrdinalIgnoreCase)?"QUOTA_OR_PLAN_LIMIT":"PROVIDER_PAYLOAD_ERROR";
                        if(safe=="QUOTA_OR_PLAN_LIMIT")await UpdateBudget(provider,remaining,limit,DateTime.UtcNow.Date.AddDays(1),ct);
                        await Audit(provider,resource,200,remaining,safe,0,ct);throw new ImportProviderException(safe);
                    }
                    if(provider=="api-football"&&resource=="status"&&data.TryGetProperty("response",out var statusBody)&&statusBody.TryGetProperty("requests",out var usage)&&usage.TryGetProperty("current",out var used)&&usage.TryGetProperty("limit_day",out var dailyLimit)&&used.TryGetInt32(out int usedCalls)&&dailyLimit.TryGetInt32(out int allowedCalls))
                        await UpdateBudget(provider,Math.Max(0,allowedCalls-usedCalls),allowedCalls,null,ct);
                    // PostgreSQL timestamps retain microseconds; normalize before hashing live and cached packages.
                    var ticks=DateTime.UtcNow.Ticks;var at=new DateTime(ticks-ticks%10,DateTimeKind.Utc);
                    if(store) {
                        var entry=await firstDb.Set<Import016ACache>().SingleOrDefaultAsync(x=>x.Key==hash,ct);
                        if(entry==null){entry=new(){Key=hash,Provider=provider,Resource=resource};firstDb.Add(entry);}
                        entry.Payload=data.GetRawText();entry.FetchedAt=at;entry.ExpiresAt=at.AddHours(Math.Clamp(options.CacheHours,1,168));entry.HttpStatus=(int)response.StatusCode;
                        await firstDb.SaveChangesAsync(ct);
                    }
                    int records=data.TryGetProperty("response",out var rows)&&rows.ValueKind==JsonValueKind.Array
                        ? rows.EnumerateArray().Any(r=>r.TryGetProperty("players",out var ps)&&ps.ValueKind==JsonValueKind.Array)?rows.EnumerateArray().Sum(r=>r.TryGetProperty("players",out var ps)&&ps.ValueKind==JsonValueKind.Array?ps.GetArrayLength():0):rows.GetArrayLength()
                        :data.TryGetProperty("entities",out var entities)&&entities.ValueKind==JsonValueKind.Object?entities.EnumerateObject().Count()
                        :data.TryGetProperty("results",out var result)&&result.TryGetProperty("bindings",out var bindings)&&bindings.ValueKind==JsonValueKind.Array?bindings.GetArrayLength():0;
                    await Audit(provider,resource,(int)response.StatusCode,remaining,"SUCCESS",records,ct);
                    return new(data.Clone(),at,false);
                } catch(OperationCanceledException) when(!ct.IsCancellationRequested) {
                    await Audit(provider,resource,null,null,"TIMEOUT",0,ct);if(attempt==options.Retries)throw new ImportProviderException("TIMEOUT");
                } catch(HttpRequestException) {
                    await Audit(provider,resource,null,null,"NETWORK_ERROR",0,ct);if(attempt==options.Retries)throw new ImportProviderException("PROVIDER_UNAVAILABLE");
                } catch(JsonException) {await Audit(provider,resource,null,null,"INVALID_JSON",0,ct);throw new ImportProviderException("INVALID_JSON");}
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2,attempt)),ct);
            }
            throw new ImportProviderException("PROVIDER_UNAVAILABLE");
        } finally {gate.Release();}
    }
    private async Task<DateTime> Reserve(string provider,bool free,CancellationToken ct) {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var now=DateTime.UtcNow;string key=provider+":"+now.ToString("yyyy-MM-dd");int limit=provider=="api-football"?Math.Clamp(options.DailyRequestBudget,1,100):1000;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_Import016ABudget\" (\"Key\",\"Used\",\"Limit\",\"Remaining\",\"BlockedUntil\",\"NextRequestAt\") VALUES ({key},0,{limit},NULL,{DateTime.UnixEpoch},{DateTime.UnixEpoch}) ON CONFLICT (\"Key\") DO NOTHING",ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))",ct);
        var state=await db.Set<Import016ABudget>().SingleAsync(x=>x.Key==key,ct);
        if(state.BlockedUntil>now)throw new ImportProviderException("PROVIDER_WAIT");
        if(!free&&(state.Used>=Math.Min(state.Limit,limit)||state.Remaining==0))throw new ImportProviderException("DAILY_BUDGET_EXHAUSTED");
        DateTime reserved=state.NextRequestAt>now?state.NextRequestAt:now;
        int interval=options.FixtureData?Math.Max(0,options.MinimumIntervalMilliseconds):provider=="api-football"?Math.Max(6100,options.MinimumIntervalMilliseconds):1100;
        state.NextRequestAt=reserved.AddMilliseconds(interval);
        if(!free){state.Used++;if(state.Remaining!=null)state.Remaining--;}
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return reserved;
    }
    private async Task UpdateBudget(string provider,int? remaining,int? limit,DateTime? blocked,CancellationToken ct) {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();string key=provider+":"+DateTime.UtcNow.ToString("yyyy-MM-dd");
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))",ct);
        var row=await db.Set<Import016ABudget>().SingleAsync(x=>x.Key==key,ct);
        if(remaining!=null)row.Remaining=Math.Max(0,Math.Min(row.Remaining??int.MaxValue,remaining.Value));
        if(limit!=null)row.Limit=Math.Max(1,Math.Min(row.Limit,limit.Value));
        if(remaining==0)row.BlockedUntil=DateTime.UtcNow.Date.AddDays(1);
        if(blocked!=null&&blocked>row.BlockedUntil)row.BlockedUntil=blocked.Value;
        await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
    }
    private async Task Audit(string provider,string resource,int? status,int? remaining,string result,int records,CancellationToken ct) {
        using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Add(new Import016AAudit{Id=Guid.NewGuid(),Provider=provider,Resource=resource,At=DateTime.UtcNow,HttpStatus=status,Remaining=remaining,Status=result,Records=records});await db.SaveChangesAsync(ct);
    }
}
