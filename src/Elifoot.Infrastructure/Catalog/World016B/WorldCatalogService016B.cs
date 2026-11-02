using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog.Import016A;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Infrastructure.Catalog.World016B;

public sealed class CountryWork016B
{
    public string Phase {get;set;}="DISCOVER";public List<string> Leagues {get;set;}=[];public int LeagueIndex {get;set;}public int TeamIndex {get;set;}
    public List<CatalogTeam> Teams {get;set;}=[];public List<CatalogAthlete> Players {get;set;}=[];public List<CatalogRank> Ranks {get;set;}=[];
    public List<CatalogRosterReport> Reports {get;set;}=[];public List<CatalogAthlete> CurrentSquad {get;set;}=[];
    public int StatsPage {get;set;}public int StatsPages {get;set;}=1;public int ReceivedPlayers {get;set;}public DateTime ObservedAt {get;set;}
}
public record ReviewCountryLeague016B(string CountryCode,string ExternalId,string Season,FootballGender Gender,string Evidence);
public record ManualClubPower016B(string ClubKey,string Season,int Power,string Evidence);
public record CountryRefreshRequest016B(string CountryCode,FootballGender Gender,string Season);
public record CountryRefreshView016B(string CountryCode,FootballGender Gender,string Season,string Status,DateTime? LastAttemptAt,
    DateTime? LastSuccessfulUpdateAt,DateTime NextEligibleUpdateAt,string? LastError,string CatalogVersion,string Phase,int CompletedTeams,int TotalTeams,bool UsingSavedData);

public sealed class WorldCatalogService016B(IServiceScopeFactory scopes,ImportHttp016A http,Import016AOptions importOptions,CatalogSyncService sync)
{
    private static readonly SemaphoreSlim ProjectionGate=new(1,1);
    private CatalogDbContext Db(IServiceScope s)=>s.ServiceProvider.GetRequiredService<CatalogDbContext>();
    private static T Read<T>(string value)=>JsonSerializer.Deserialize<T>(value,CatalogImportCodec.Json)!;
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,CatalogImportCodec.Json);
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,CatalogImportCodec.Json)));
    public static readonly Dictionary<string,(string Local,string Provider)> Countries=new(StringComparer.Ordinal) {
        ["ES"]=("Espanha","Spain"),["GB"]=("Inglaterra","England"),["BR"]=("Brasil","Brazil"),["AR"]=("Argentina","Argentina"),
        ["DE"]=("Alemanha","Germany"),["IT"]=("Itália","Italy"),["FR"]=("França","France"),["PT"]=("Portugal","Portugal")};
    private static string Key(string country,FootballGender gender,string season)=>$"{country}|{gender}|{season}";
    private static void Validate(string country,FootballGender gender,string season) {
        if(!System.Text.RegularExpressions.Regex.IsMatch(country??"","^[A-Z]{2,3}$")||!Enum.IsDefined(gender)||!int.TryParse(season,out int year)||year is <1900 or >2100)
            throw new ArgumentException("País, categoria ou temporada inválidos. Escolha um país do catálogo disponível.");
    }
    public async Task<List<CatalogCountryEntry>> Directory(CancellationToken ct=default) {
        using var scope=scopes.CreateScope();var db=Db(scope);var countries=await db.Set<CountryDirectory016B>().AsNoTracking().ToListAsync(ct);
        return countries.Select(c=>new CatalogCountryEntry(c.Code,c.ProviderName)).Concat(Countries.Select(c=>new CatalogCountryEntry(c.Key,c.Value.Local)))
            .GroupBy(c=>c.Code).Select(g=>g.Last()).OrderBy(c=>c.Name,StringComparer.Ordinal).ToList();
    }
    private static async Task<(string Local,string Provider)> Country(string code,CatalogDbContext db,CancellationToken ct) {
        if(Countries.TryGetValue(code,out var known))return known;
        var row=await db.Set<CountryDirectory016B>().AsNoTracking().SingleOrDefaultAsync(c=>c.Code==code,ct)??throw new ArgumentException("País ainda não descoberto na API.");
        return(row.ProviderName,row.ProviderName);
    }
    private static CountryRefreshView016B View(CountryCatalog016B row) {
        var work=Read<CountryWork016B>(row.WorkJson);
        return new(row.CountryCode,row.Gender,row.Season,row.Status,row.LastAttemptAt,row.LastSuccessfulUpdateAt,row.NextEligibleUpdateAt,
            row.LastError,row.CatalogVersion,work.Phase,work.TeamIndex,work.Teams.Count,row.LastSuccessfulUpdateAt!=null||!string.IsNullOrEmpty(row.CatalogVersion));
    }
    public async Task<List<CountryRefreshView016B>> Status(FootballGender gender,string season,CancellationToken ct=default) {
        using var scope=scopes.CreateScope();var db=Db(scope);
        var rows=await db.Set<CountryCatalog016B>().AsNoTracking().Where(x=>x.Gender==gender&&x.Season==season).ToListAsync(ct);
        return (await Directory(ct)).Select(c=>c.Code).Select(c=>rows.SingleOrDefault(x=>x.CountryCode==c)??new(){CountryCode=c,Gender=gender,Season=season,Status="NOT_REQUESTED"}).Select(View).ToList();
    }
    public async Task<object> Report(string country,FootballGender gender,string season,CancellationToken ct=default) {
        Validate(country,gender,season);using var scope=scopes.CreateScope();var db=Db(scope);
        var row=await db.Set<CountryCatalog016B>().AsNoTracking().SingleOrDefaultAsync(x=>x.Key==Key(country,gender,season),ct);
        var leagues=await db.Set<CountryLeague016B>().AsNoTracking().Where(x=>x.CountryCode==country&&x.Season==season).OrderBy(x=>x.Name).ToListAsync(ct);
        var creator=scope.ServiceProvider.GetRequiredService<WorldCreatorService>();var coverage=await creator.Coverage([country],gender,season,ct);
        var selection=await creator.SelectForWorld(new("Catalog report",gender,season,[country],WorldAssemblyMode.Automatic,DistrictFillMode.Ranking,1,[],UseSavedData:true,UseWorldCatalogRules:true),ct);
        return new {Status=row==null?null:View(row),Leagues=leagues,Coverage=coverage,Provider=await http.Status("api-football",ct),
            PositionCoverage=selection.Clubs.SelectMany(c=>c.Players).GroupBy(p=>p.Position).ToDictionary(g=>g.Key,g=>g.Count()),
            ColorsCoverage=selection.Clubs.Count(c=>c.ColorPalette?.ColorVerified==true),RankingCoverage=selection.Clubs.Count(c=>c.InitialProfile?.RankingConfidence>0),
            HistoricalPlayers=selection.Clubs.Sum(c=>c.ReferencePlayers?.Count??0),
            ClubProfiles=selection.Clubs.Select(c=>new {c.Key,c.Name,c.InitialProfile,Players=c.Players.Count,Historical=c.ReferencePlayers?.Count??0,ColorsVerified=c.ColorPalette?.ColorVerified==true})};
    }
    public async Task<CountryRefreshView016B> Queue(string country,FootballGender gender,string season,CancellationToken ct=default) {
        Validate(country,gender,season);using var scope=scopes.CreateScope();var db=Db(scope);await Country(country,db,ct);string key=Key(country,gender,season);
        // Cross-process uniqueness; a second browser cannot create another country job.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_CountryCatalog016B\" (\"Key\",\"CountryCode\",\"Gender\",\"Season\",\"Status\",\"NextEligibleUpdateAt\",\"NextAttemptAt\",\"CatalogVersion\",\"WorkJson\") VALUES ({key},{country},{(int)gender},{season},{"QUEUED"},{DateTime.UnixEpoch},{DateTime.UnixEpoch},{""},{"{}"}) ON CONFLICT (\"Key\") DO NOTHING",ct);
        var row=await db.Set<CountryCatalog016B>().AsNoTracking().SingleAsync(x=>x.Key==key,ct);var now=DateTime.UtcNow;
        if(row.LastSuccessfulUpdateAt>now.AddDays(-7))return View(row) with {Status="CACHED",UsingSavedData=true};
        if(row.LeaseUntil>now || row.Status=="PARTIAL"&&row.NextAttemptAt>now)return View(row);
        string state=http.HasApiKey?"QUEUED":"NOT_CONNECTED";
        // Completed runs start a fresh pass; interrupted runs retain their per-team/page cursor.
        var work=row.LastSuccessfulUpdateAt!=null&&Read<CountryWork016B>(row.WorkJson).Phase=="DONE"?"{}":row.WorkJson;
        await db.Set<CountryCatalog016B>().Where(x=>x.Key==key&&(x.LeaseUntil==null||x.LeaseUntil<=now))
            .ExecuteUpdateAsync(x=>x.SetProperty(v=>v.Status,state).SetProperty(v=>v.WorkJson,work)
                .SetProperty(v=>v.NextAttemptAt,now).SetProperty(v=>v.LastError,http.HasApiKey?null:"NOT_CONNECTED"),ct);
        row=await db.Set<CountryCatalog016B>().AsNoTracking().SingleAsync(x=>x.Key==key,ct);return View(row);
    }
    public async Task ReviewLeague(ReviewCountryLeague016B request,CancellationToken ct=default) {
        Validate(request.CountryCode,request.Gender,request.Season);
        if(string.IsNullOrWhiteSpace(request.Evidence)||request.Evidence.Length>2000)throw new ArgumentException("A categoria exige evidência revisada da liga.");
        using var scope=scopes.CreateScope();var db=Db(scope);
        var league=await db.Set<CountryLeague016B>().SingleOrDefaultAsync(x=>x.CountryCode==request.CountryCode&&x.ExternalId==request.ExternalId&&x.Season==request.Season,ct)
            ??throw new ArgumentException("Liga ainda não descoberta no fornecedor; IDs não são inventados.");
        if(league.Gender!=null&&league.Gender!=request.Gender)throw new ArgumentException("Categoria já revisada; não é permitido reinterpretar a mesma liga como masculina e feminina.");
        league.Gender=request.Gender;league.CategoryEvidence=request.Evidence.Trim();await db.SaveChangesAsync(ct);
        await Queue(request.CountryCode,request.Gender,request.Season,ct);
    }
    public async Task<bool> RunOneStep(CancellationToken ct=default) {
        using var scope=scopes.CreateScope();var db=Db(scope);var now=DateTime.UtcNow;var owner=Guid.NewGuid();
        var key=await db.Set<CountryCatalog016B>().Where(x=>(x.LeaseUntil==null||x.LeaseUntil<=now)&&x.NextAttemptAt<=now&&
            (x.Status=="QUEUED"||x.Status=="UPDATING"||x.Status=="PARTIAL"||x.Status=="FAILED"||
                (x.Status=="SUCCEEDED"&&x.NextEligibleUpdateAt<=now)))
            .OrderBy(x=>x.NextAttemptAt).ThenBy(x=>x.Key).Select(x=>x.Key).FirstOrDefaultAsync(ct);
        if(key==null)return false;
        int claim=await db.Set<CountryCatalog016B>().Where(x=>x.Key==key&&(x.LeaseUntil==null||x.LeaseUntil<=now))
            .ExecuteUpdateAsync(x=>x.SetProperty(v=>v.LeaseOwner,owner).SetProperty(v=>v.LeaseUntil,now.AddMinutes(5))
                .SetProperty(v=>v.Status,"UPDATING").SetProperty(v=>v.LastAttemptAt,now),ct);
        if(claim==0)return false;
        var row=await db.Set<CountryCatalog016B>().SingleAsync(x=>x.Key==key,ct);var work=Read<CountryWork016B>(row.WorkJson);
        if(work.Phase=="DONE")work=new(); // Successful countries become due again without a new browser request.
        try {
            if(!http.HasApiKey)throw new ImportProviderException("NOT_CONNECTED");
            await Step(db,row,work,ct);
            row.WorkJson=Json(work);row.LastError=null;
            if(work.Phase=="DONE") {
                row.Status="SUCCEEDED";row.LastSuccessfulUpdateAt=DateTime.UtcNow;row.NextEligibleUpdateAt=DateTime.UtcNow.AddDays(7);
                row.CatalogVersion=Hash(work);row.NextAttemptAt=row.NextEligibleUpdateAt;
            } else {row.Status="QUEUED";row.NextAttemptAt=DateTime.UtcNow;}
        } catch(OperationCanceledException) when(ct.IsCancellationRequested) {
            row.WorkJson=Json(work);row.Status="QUEUED";row.NextAttemptAt=DateTime.UtcNow;row.LastError="CANCELLED_SAFE_CHECKPOINT";
        } catch(Exception error) {
            string code=error is ImportProviderException provider?provider.Code:error is ArgumentException?"INVALID_PROVIDER_DATA":error is CatalogBusyException?"CATALOG_BUSY":error is DbUpdateException?"CATALOG_WRITE_FAILED":"PROVIDER_UNAVAILABLE";
            row.WorkJson=Json(work);row.LastError=code;
            row.Status=code is "NOT_CONNECTED" or "ACCESS_RIGHTS_NOT_CONFIRMED" or "REVIEW_REQUIRED" or "CURRENT_SEASON_UNAVAILABLE"?code:
                code is "DAILY_BUDGET_EXHAUSTED" or "RATE_LIMIT" or "QUOTA_OR_PLAN_LIMIT" or "PROVIDER_WAIT"?"PARTIAL":"FAILED";
            var budget=await http.Status("api-football",CancellationToken.None);
            row.NextAttemptAt=budget.BlockedUntil ?? (row.Status=="PARTIAL"?DateTime.UtcNow.Date.AddDays(1):DateTime.UtcNow.AddMinutes(15));
        } finally {
            row.LeaseOwner=null;row.LeaseUntil=null;await db.SaveChangesAsync(CancellationToken.None);
        }
        return true;
    }
    private async Task Step(CatalogDbContext db,CountryCatalog016B row,CountryWork016B work,CancellationToken ct) {
        string country=row.CountryCode,season=row.Season;var gender=row.Gender;var countryInfo=await Country(country,db,ct);
        async Task<ImportResponse016A> Get(string resource)=>await http.Get("api-football",resource,false,ct);
        if(work.Phase=="DISCOVER") {
            var countryData=await Get("countries");
            var countryRows=Rows(countryData.Data,"response").Where(x=>System.Text.RegularExpressions.Regex.IsMatch(Text(x,"code"),"^[A-Z]{2,3}$")).GroupBy(x=>Text(x,"code"));
            foreach(var group in countryRows) {
                var valid=group.Where(x=>!Countries.TryGetValue(group.Key,out var fixedCountry)||Text(x,"name")==fixedCountry.Provider).ToList();
                if(valid.Count!=1)continue;string providerName=Text(valid[0],"name");if(string.IsNullOrWhiteSpace(providerName))continue;
                var directory=await db.Set<CountryDirectory016B>().SingleOrDefaultAsync(x=>x.Code==group.Key,ct);
                if(directory==null){directory=new(){Code=group.Key};db.Add(directory);}
                directory.ProviderName=providerName;directory.ObservedAt=countryData.FetchedAt;
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_CatalogCountry\" (\"Code\",\"Name\") VALUES ({group.Key},{providerName}) ON CONFLICT (\"Code\") DO NOTHING",ct);
            }
            await db.SaveChangesAsync(ct);
            if(!Rows(countryData.Data,"response").Any(x=>Text(x,"name")==countryInfo.Provider))throw new ImportProviderException("COUNTRY_NOT_COVERED");
            var data=await Get($"leagues?country={Uri.EscapeDataString(countryInfo.Provider)}&season={season}");
            foreach(var item in Rows(data.Data,"response").Where(x=>Text(Child(x,"league"),"type")=="League")) {
                var league=Child(item,"league");string id=Text(league,"id");if(!Numeric(id))continue;
                var years=Rows(item,"seasons").Where(x=>Text(x,"year")==season).ToList();if(years.Count!=1)continue;
                string leagueKey=$"{country}|{id}|{season}";
                var record=await db.Set<CountryLeague016B>().SingleOrDefaultAsync(x=>x.Key==leagueKey,ct);
                if(record==null){record=new(){Key=leagueKey,CountryCode=country,ExternalId=id,Season=season};db.Add(record);}
                record.Name=Text(league,"name");record.CurrentSeason=Bool(years[0],"current");
                record.Standings=Bool(Child(years[0],"coverage"),"standings");record.PlayerStatistics=Bool(Child(years[0],"coverage"),"players");record.ObservedAt=data.FetchedAt;
            }
            await db.SaveChangesAsync(ct);
            work.Leagues=await db.Set<CountryLeague016B>().Where(x=>x.CountryCode==country&&x.Season==season&&x.Gender==gender&&x.CategoryEvidence!=null).OrderBy(x=>x.ExternalId).Select(x=>x.ExternalId).ToListAsync(ct);
            if(work.Leagues.Count==0)throw new ImportProviderException("REVIEW_REQUIRED");
            work.LeagueIndex=0;work.Phase="TEAMS";return;
        }
        if(work.LeagueIndex>=work.Leagues.Count){work.Phase="DONE";return;}
        string leagueId=work.Leagues[work.LeagueIndex];
        var approved=await db.Set<CountryLeague016B>().AsNoTracking().SingleAsync(x=>x.CountryCode==country&&x.ExternalId==leagueId&&x.Season==season&&x.Gender==gender,ct);
        if(work.Phase=="TEAMS") {
            var response=await Get($"teams?league={leagueId}&season={season}");work.ObservedAt=response.FetchedAt;
            work.Teams=Rows(response.Data,"response").Select(x=>Child(x,"team")).Where(x=>Numeric(Text(x,"id"))&&!Bool(x,"national")&&Text(x,"country")==countryInfo.Provider)
                .Select(x=>new CatalogTeam(Text(x,"id"),Text(x,"name"),string.IsNullOrWhiteSpace(Text(x,"code"))?Text(x,"name")[..Math.Min(5,Text(x,"name").Length)]:Text(x,"code"),leagueId,approved.Name,
                    Verification:new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,$"API-Football teams?league={leagueId}&season={season}; category reviewed: {approved.CategoryEvidence}",response.FetchedAt,season,approved.CurrentSeason)))
                .Where(x=>!string.IsNullOrWhiteSpace(x.Name)).DistinctBy(x=>x.ExternalId).OrderBy(x=>x.ExternalId,StringComparer.Ordinal).ToList();
            if(work.Teams.Count==0)throw new ImportProviderException("EMPTY_TEAM_RESPONSE");
            work.Players=[];work.Reports=[];work.Ranks=[];work.TeamIndex=0;work.StatsPage=0;
            await Publish(row,work,CatalogDataType.Clubs,ct);
            work.Phase=approved.Standings?"STANDINGS":"SQUADS";return;
        }
        if(work.Phase=="STANDINGS") {
            var response=await Get($"standings?league={leagueId}&season={season}");
            var table=Rows(response.Data,"response").SelectMany(x=>Rows(Child(x,"league"),"standings")).Where(x=>x.ValueKind==JsonValueKind.Array).SelectMany(x=>x.EnumerateArray()).ToList();
            var known=work.Teams.Select(t=>t.ExternalId).ToHashSet();
            work.Ranks=table.Where(x=>known.Contains(Text(Child(x,"team"),"id"))).GroupBy(x=>Text(Child(x,"team"),"id"))
                .Select(g=>g.OrderByDescending(x=>Number(x,"points")??0).First()).Select(x=> {
                    int played=(int)(Number(Child(x,"all"),"played")??0);double? score=played>0?(Number(x,"points")??0)/(3*played):null;
                    return new CatalogRank(Text(Child(x,"team"),"id"),"api-football",gender,season,Math.Max(1,(int)(Number(x,"rank")??1)),"api-football",response.FetchedAt,
                        score,"elifoot-standings-points-ratio-v1-disconnected",score==null?0:.2,$"elifoot-estimated:{gender}:{season}",CatalogDataOrigin.Estimated);
                }).ToList();
            if(work.Ranks.Count>0)await Publish(row,work,CatalogDataType.Rankings,ct);
            work.Phase="SQUADS";return;
        }
        if(work.Phase=="SQUADS") {
            if(!approved.CurrentSeason)throw new ImportProviderException("CURRENT_SEASON_UNAVAILABLE");
            if(work.TeamIndex>=work.Teams.Count){work.LeagueIndex++;work.Phase="TEAMS";return;}
            var team=work.Teams[work.TeamIndex];var response=await Get("players/squads?team="+team.ExternalId);
            var squads=Rows(response.Data,"response").Where(x=>Text(Child(x,"team"),"id")==team.ExternalId).ToList();
            if(squads.Count!=1)throw new ImportProviderException("SQUAD_TEAM_MISMATCH");
            var players=Rows(squads[0],"players").ToList();work.ReceivedPlayers=players.Count;
            work.CurrentSquad=players.Where(x=>Numeric(Text(x,"id"))&&!string.IsNullOrWhiteSpace(Text(x,"name"))).Select(x=> {
                string? role=Text(x,"position") switch {"Goalkeeper"=>"G","Defender"=>"D","Midfielder"=>"M","Attacker"=>"A",_=>null};
                string id=Text(x,"id");return role==null?null:new CatalogAthlete(id,team.ExternalId,"api-football",gender,season,Text(x,"name"),role,26,"api-football",response.FetchedAt,
                    $"api-football:{gender}:{id}",CatalogDataOrigin.Real,new(InitialPlayerStrength.Version+":missing-individual",CatalogDataOrigin.Estimated,.2,Hash(new{id,team.ExternalId}),.5,null),
                    OriginalPosition:Text(x,"position"),MembershipEvidence:new(CatalogMembershipStatus.ProviderReported,$"api-football:squad:{team.ExternalId}:{id}",
                        $"https://v3.football.api-sports.io/players/squads?team={team.ExternalId}; observed {response.FetchedAt:o}",DateOnly.FromDateTime(response.FetchedAt)),PlayerOrigin:WorldPlayerOrigin.REAL_CURRENT_PROVIDER_REPORTED);
            }).Where(x=>x!=null).Select(x=>x!).DistinctBy(x=>x.PlayerExternalId).ToList();
            if(work.CurrentSquad.Count==0)throw new ImportProviderException("EMPTY_VALID_SQUAD");
            var options=scopes.CreateScope();using(options) {
                if(options.ServiceProvider.GetService<WorldCatalogOptions016B>()?.FetchPlayerStatistics==true && approved.PlayerStatistics) {work.StatsPage=1;work.StatsPages=1;work.Phase="STATISTICS";return;}
            }
            FinishTeam(work);return;
        }
        if(work.Phase=="STATISTICS") {
            var team=work.Teams[work.TeamIndex];var response=await Get($"players?team={team.ExternalId}&season={season}&page={work.StatsPage}");
            int page=(int)(Number(Child(response.Data,"paging"),"current")??0),total=(int)(Number(Child(response.Data,"paging"),"total")??0);
            if(page!=work.StatsPage||total is <1 or >100)throw new ImportProviderException("INVALID_PLAYER_PAGINATION");
            foreach(var rowData in Rows(response.Data,"response")) {
                string id=Text(Child(rowData,"player"),"id");var current=work.CurrentSquad.FindIndex(p=>p.PlayerExternalId==id);if(current<0)continue;
                var stats=Rows(rowData,"statistics").FirstOrDefault(x=>Text(Child(x,"team"),"id")==team.ExternalId&&Text(Child(x,"league"),"id")==leagueId&&Text(Child(x,"league"),"season")==season);
                if(stats.ValueKind==JsonValueKind.Undefined)continue;
                var games=Child(stats,"games");var goals=Child(stats,"goals");
                var perf=new CatalogPlayerPerformance(Number(games,"rating"),Integer(games,"minutes"),Integer(games,"appearences"),Integer(goals,"total"),Integer(goals,"assists"),"api-football-v3-position-rating");
                work.CurrentSquad[current]=work.CurrentSquad[current] with {Performance=perf};
            }
            work.StatsPages=total;if(work.StatsPage++<total)return;FinishTeam(work);return;
        }
        if(work.Phase=="PUBLISH") {
            // A player's current report is unique inside a snapshot; prior versions remain historical.
            work.Players=work.Players.GroupBy(p=>p.PlayerExternalId).Select(g=>g.OrderByDescending(p=>p.UpdatedAt).ThenBy(p=>p.ClubExternalId,StringComparer.Ordinal).First()).ToList();
            work.Reports=work.Reports.Select(report=>report with {UsablePlayers=work.Players.Count(p=>p.ClubExternalId==report.ClubExternalId)}).ToList();
            await Publish(row,work,CatalogDataType.Squads,ct);work.LeagueIndex++;work.Phase="TEAMS";
        }
    }
    private static void FinishTeam(CountryWork016B work) {
        string team=work.Teams[work.TeamIndex].ExternalId;work.Players.AddRange(work.CurrentSquad);
        work.Reports.Add(new(team,"api-football",work.ReceivedPlayers,work.CurrentSquad.Count,PlayableWorldPolicy.Complete(work.CurrentSquad),
            work.ReceivedPlayers!=work.CurrentSquad.Count?["Unknown positions or duplicate IDs excluded; no real identity invented."]:[]));
        work.TeamIndex++;work.StatsPage=0;work.CurrentSquad=[];work.Phase=work.TeamIndex>=work.Teams.Count?"PUBLISH":"SQUADS";
    }
    private async Task Publish(CountryCatalog016B row,CountryWork016B work,CatalogDataType type,CancellationToken ct) {
        var rights=importOptions.ApiFootballRights??throw new ImportProviderException("ACCESS_RIGHTS_NOT_CONFIRMED");
        var date=new[]{work.ObservedAt}.Concat(work.Players.Select(p=>p.UpdatedAt)).Concat(work.Ranks.Select(r=>r.UpdatedAt)).Max();
        int count=type==CatalogDataType.Clubs?work.Teams.Count:type==CatalogDataType.Squads?work.Players.Count:work.Ranks.Count;
        using var countryScope=scopes.CreateScope();var countryInfo=await Country(row.CountryCode,Db(countryScope),ct);
        var package=new CatalogImportPackage(new(row.CountryCode,row.Gender,"016b:"+work.Leagues[work.LeagueIndex],row.Season,type,"api-football"),countryInfo.Local,
            Hash(new{type,work.Teams,work.Players,work.Ranks}),date,true,1,1,count,rights.StorageAllowed,rights.CommercialUseAllowed,rights.License,
            importOptions.FixtureData,type==CatalogDataType.Clubs?work.Teams:[],type==CatalogDataType.Squads?work.Players:[],type==CatalogDataType.Rankings?work.Ranks:[],
            Rights:rights,AllowPartialSquads:type==CatalogDataType.Squads,RosterReports:type==CatalogDataType.Squads?work.Reports:null,RosterAsOf:type==CatalogDataType.Squads?DateOnly.FromDateTime(date):null);
        if(CatalogRightsPolicy.StorageIssue(package,DateTime.UtcNow)!=null)throw new ImportProviderException("STORAGE_RIGHTS_NOT_CONFIRMED");
        await sync.ImportAsync(package,ct);
        row.CatalogVersion=package.Version;
    }
    public async Task ManualPower(ManualClubPower016B command,CancellationToken ct=default) {
        if(command.Power is <1 or >20 || string.IsNullOrWhiteSpace(command.Evidence) || command.Evidence.Length>2000)throw new ArgumentException("Força deve ser de 1 a 20 e exige evidência da atribuição manual.");
        await ProjectionGate.WaitAsync(ct);try {
            using var scope=scopes.CreateScope();var db=Db(scope);string key=command.ClubKey+"|"+command.Season;
            var row=await db.Set<WorldClubProjection016B>().SingleOrDefaultAsync(x=>x.Key==key,ct)??throw new ArgumentException("Consulte o catálogo antes de revisar a força de um clube existente.");
            row.ProfileJson=Json(Read<WorldClubProfile016B>(row.ProfileJson) with {ClubInitialPower=command.Power,ManualPower=true,ManualPowerEvidence=command.Evidence.Trim()});
            row.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);
        }finally{ProjectionGate.Release();}
    }
    public async Task<List<EligibleCatalogClub>> Project(List<EligibleCatalogClub> clubs,CancellationToken ct=default) {
        await ProjectionGate.WaitAsync(ct);try {
            using var scope=scopes.CreateScope();var db=Db(scope);
            var saved=await db.Set<WorldClubProjection016B>().ToDictionaryAsync(x=>x.Key,ct);
            var ranked=WorldCatalogRules016B.Classify(clubs.Select(c=>saved.TryGetValue(c.Key+"|"+c.Season,out var old)?c with {InitialProfile=Read<WorldClubProfile016B>(old.ProfileJson)}:c));
            foreach(var c in ranked){if(!saved.TryGetValue(c.Key+"|"+c.Season,out var row)){row=new(){Key=c.Key+"|"+c.Season};db.Add(row);}row.ProfileJson=Json(c.InitialProfile);row.UpdatedAt=DateTime.UtcNow;}
            await db.SaveChangesAsync(ct);return ranked;
        }finally{ProjectionGate.Release();}
    }
    private static JsonElement Child(JsonElement x,string key)=>x.ValueKind==JsonValueKind.Object&&x.TryGetProperty(key,out var v)?v:default;
    private static IEnumerable<JsonElement> Rows(JsonElement x,string key)=>Child(x,key).ValueKind==JsonValueKind.Array?Child(x,key).EnumerateArray():Enumerable.Empty<JsonElement>();
    private static string Text(JsonElement x,string key)=>Child(x,key).ValueKind is JsonValueKind.String?Child(x,key).GetString()??"":Child(x,key).ValueKind==JsonValueKind.Number?Child(x,key).GetRawText():"";
    private static bool Bool(JsonElement x,string key)=>Child(x,key).ValueKind==JsonValueKind.True;
    private static bool Numeric(string x)=>long.TryParse(x,out long id)&&id>0;
    private static double? Number(JsonElement x,string key)=>double.TryParse(Text(x,key),NumberStyles.Float,CultureInfo.InvariantCulture,out double n)&&double.IsFinite(n)&&n>=0?n:null;
    private static int? Integer(JsonElement x,string key)=>Number(x,key) is double v?(int)Math.Min(int.MaxValue,v):null;
}
