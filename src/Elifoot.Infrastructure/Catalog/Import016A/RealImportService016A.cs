using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Infrastructure.Catalog.Import016A;

public sealed class RealImportService016A(IServiceScopeFactory scopes,PilotData016A source,ImportHttp016A http,CatalogSyncService sync,Import016AOptions options)
{
    private static readonly SemaphoreSlim ApplyGate=new(1,1);
    private CatalogDbContext Db(IServiceScope s)=>s.ServiceProvider.GetRequiredService<CatalogDbContext>();
    private static T Read<T>(string json)=>JsonSerializer.Deserialize<T>(json,CatalogImportCodec.Json)!;
    private static string Write<T>(T item)=>JsonSerializer.Serialize(item,CatalogImportCodec.Json);
    public async Task<RealImportCoverage016A> Coverage(CancellationToken ct=default) {
        using var s=scopes.CreateScope();var db=Db(s);var runs=await db.Set<Import016ARun>().AsNoTracking().OrderByDescending(r=>r.CreatedAt).Take(50).ToListAsync(ct);
        // Identity-only previews do not replace the last collected squad coverage.
        var clubs=runs.Select(r=>Read<RealImportRun016A>(r.ReportJson)).Where(r=>!r.IsFixture).SelectMany(r=>r.Clubs)
            .GroupBy(c=>(c.ClubKey,c.Provider)).Select(g=>g.FirstOrDefault(c=>!c.Issues.Any(i=>i.StartsWith("SQUADS_NOT_REQUESTED",StringComparison.Ordinal)))??g.First()).ToList();
        return new([await http.Status("api-football",ct),await http.Status("wikidata",ct)],clubs,runs.FirstOrDefault()?.CreatedAt,
            clubs.Sum(c=>c.Players.Count),clubs.Sum(c=>c.CurrentReported),clubs.Sum(c=>c.CurrentVerified),clubs.Sum(c=>c.Players.Count(p=>p.Issues.Count>0)));
    }
    public async Task<RealImportRun016A> Report(Guid id,CancellationToken ct=default) {
        using var s=scopes.CreateScope();var row=await Db(s).Set<Import016ARun>().AsNoTracking().SingleOrDefaultAsync(r=>r.Id==id,ct)??throw new KeyNotFoundException("Relatório não encontrado.");
        return Read<RealImportRun016A>(row.ReportJson);
    }
    public async Task Map(ConfirmClub016ARequest request,CancellationToken ct=default) {
        var club=PilotClubs016A.All.SingleOrDefault(c=>c.Key==request.ClubKey)??throw new ArgumentException("Clube fora do piloto.");
        if(request.Provider!="api-football"||request.Gender!=club.Gender||request.CountryCode!=club.CountryCode||!System.Text.RegularExpressions.Regex.IsMatch(request.ExternalTeamId,"^[1-9][0-9]*$")||!System.Text.RegularExpressions.Regex.IsMatch(request.ExternalLeagueId,"^[1-9][0-9]*$")||string.IsNullOrWhiteSpace(request.LeagueName)||request.Evidence.Length<16||request.Evidence.Length>1000)throw new ArgumentException("Confirme IDs, país, categoria, liga e evidência.");
        using var s=scopes.CreateScope();var db=Db(s);string key=request.Provider+":"+club.Key;
        if(await db.Set<Import016AMapping>().AnyAsync(m=>m.Provider==request.Provider&&m.TeamId==request.ExternalTeamId&&m.ClubKey!=club.Key,ct))throw new ArgumentException("ID já atribuído a outro clube/categoria; revisão necessária.");
        var row=await db.Set<Import016AMapping>().SingleOrDefaultAsync(m=>m.Key==key,ct);
        if(row==null){row=new(){Key=key,ClubKey=club.Key,Provider=request.Provider};db.Add(row);}
        row.TeamId=request.ExternalTeamId;row.LeagueId=request.ExternalLeagueId;row.LeagueName=request.LeagueName;row.Evidence=request.Evidence;row.ReviewedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);
    }
    public async Task<RealImportRun016A> DryRun(RealImport016ARequest request,CancellationToken ct=default) {
        if(request.Provider is not ("wikidata" or "api-football")||request.ClubKeys.Count is <1 or >6||request.ClubKeys.Distinct().Count()!=request.ClubKeys.Count||request.ClubKeys.Any(k=>PilotClubs016A.All.All(c=>c.Key!=k)))throw new ArgumentException("Selecione de um a seis clubes do piloto e um fornecedor válido.");
        var reports=new List<ImportClubReport016A>();var packages=new List<CatalogImportPackage>();var runIssues=new List<string>();
        foreach(var key in request.ClubKeys) {
            var club=PilotClubs016A.All.Single(c=>c.Key==key);
            try {
                using var s=scopes.CreateScope();var mapping=await Db(s).Set<Import016AMapping>().AsNoTracking().SingleOrDefaultAsync(m=>m.Key==request.Provider+":"+key,ct);
                var data=request.Provider=="wikidata"?await source.Wikidata(club,request.Refresh,ct,request.IncludeSquads):await source.ApiFootball(club,mapping,request.Refresh,ct,request.IncludeSquads);
                reports.Add(data.Report);packages.AddRange(data.Packages);
            } catch(ImportProviderException e) {
                reports.Add(new(club.Key,club.Name,club.CountryCode,club.Gender,request.Provider,null,club.WikidataId,"UNCONFIRMED",DateTime.UtcNow,e.Code,0,0,[],0,0,0,0,[e.Code],[],[]));runIssues.Add(club.Key+":"+e.Code);
            }
        }
        string status=reports.All(c=>c.Status=="CURRENT_PROVIDER_REPORTED")?"READY":packages.Count>0?"INCOMPLETE":"NOT_APPLICABLE";
        var result=new RealImportRun016A(Guid.NewGuid(),"DRY_RUN",request.Provider,status,DateTime.UtcNow,reports,runIssues,options.FixtureData);
        using(var s=scopes.CreateScope()){var db=Db(s);db.Add(new Import016ARun{Id=result.Id,CreatedAt=result.CreatedAt,ReportJson=Write(result),PackagesJson=Write(packages)});await db.SaveChangesAsync(ct);}
        return result;
    }
    public async Task<RealImportRun016A> Apply(Guid id,CancellationToken ct=default) {
        await ApplyGate.WaitAsync(ct);
        try {
            using var s=scopes.CreateScope();var db=Db(s);var row=await db.Set<Import016ARun>().SingleOrDefaultAsync(r=>r.Id==id,ct)??throw new KeyNotFoundException("Prévia não encontrada.");
            var report=Read<RealImportRun016A>(row.ReportJson);if(row.Applied)return report;
            if(row.CreatedAt<DateTime.UtcNow.AddDays(-1))throw new ArgumentException("Prévia expirada. Execute nova importação explicitamente.");
            if(report.IsFixture!=options.FixtureData)throw new ArgumentException("Não aplicar fixture de outro ambiente.");
            var packages=Read<List<CatalogImportPackage>>(row.PackagesJson);if(packages.Count==0)throw new ArgumentException("Sem dados validados para aplicar. Consulte o relatório.");
            if(packages.Any(p=>CatalogRightsPolicy.StorageIssue(p,DateTime.UtcNow)!=null))throw new ArgumentException("Direitos de armazenamento não confirmados.");
            // Find existing clubs by proven Wikidata linkage/provider alias, never merge by similar names.
            var snapshots=await db.Snapshots.AsNoTracking().Where(x=>db.Scopes.Any(scope=>scope.ActiveSnapshotId==x.Id&&scope.DataType==CatalogDataType.Clubs)).ToListAsync(ct);
            var existing=snapshots.SelectMany(x=>Read<CatalogImportPackage>(x.PayloadJson).Clubs.Select(c=>(Package:Read<CatalogImportPackage>(x.PayloadJson),Team:c))).ToList();
            var aliases=await db.Set<CatalogClubIdentityAlias>().AsNoTracking().ToListAsync(ct);
            var links=new List<CatalogIdentityMappingRequest>();var prepared=new List<CatalogImportPackage>();
            foreach(var p in packages) {
                if(p.Scope.DataType!=CatalogDataType.Clubs){prepared.Add(p);continue;}
                var teams=new List<CatalogTeam>();
                foreach(var team in p.Clubs) {
                    var pilot=PilotClubs016A.All.Single(c=>c.WikidataId==team.Verification!.WikidataId&&c.Gender==p.Scope.Gender);
                    var currentAlias=aliases.SingleOrDefault(a=>a.Key==CatalogIdentityService.AliasKey(p.Scope.CountryCode,p.Scope.Gender,p.Scope.Provider,team.ExternalId));
                    var proven=existing.Where(e=>e.Package.Scope.CountryCode==p.Scope.CountryCode&&e.Package.Scope.Gender==p.Scope.Gender&&
                        (e.Team.Verification?.WikidataId==pilot.WikidataId||e.Package.Scope.Provider==p.Scope.Provider&&e.Team.ExternalId==team.ExternalId||
                        currentAlias!=null&&aliases.Any(a=>a.CountryCode==p.Scope.CountryCode&&a.Gender==p.Scope.Gender&&a.Provider==e.Package.Scope.Provider&&a.ExternalId==e.Team.ExternalId&&a.ClubId==currentAlias.ClubId))).ToList();
                    var targets=proven.Select(e=>aliases.SingleOrDefault(a=>a.CountryCode==p.Scope.CountryCode&&a.Gender==p.Scope.Gender&&a.Provider==e.Package.Scope.Provider&&a.ExternalId==e.Team.ExternalId)?.ClubId).Where(x=>x!=null).Distinct().ToList();

                    if(targets.Count>1||targets.Count==1&&currentAlias!=null&&currentAlias.ClubId!=targets[0])throw new ArgumentException("IDENTITY_CONFLICT: revisão explícita necessária.");
                    if(targets.Count==0 && existing.Any(e=>e.Package.Scope.CountryCode==p.Scope.CountryCode&&e.Package.Scope.Gender==p.Scope.Gender&&pilot.Aliases.Append(pilot.Name).Any(n=>Normalize(n)==Normalize(e.Team.Name))))
                        throw new ArgumentException("IDENTITY_LINK_REQUIRED: clube existente sem vínculo externo comprovado. Revise a associação antes de aplicar.");
                    if(targets.Count==1)links.Add(new(p.Scope.CountryCode,p.Scope.Gender,p.Scope.Provider,team.ExternalId,targets[0]!.Value,"016A Wikidata identity: "+pilot.WikidataId));
                    var prior=proven.OrderByDescending(e=>e.Package.UpdatedAt).FirstOrDefault().Team;
                    teams.Add(prior==null?team:team with {PrimaryColor=prior.PrimaryColor,SecondaryColor=prior.SecondaryColor,ColorPalette=prior.ColorPalette});
                }
                var enriched=p with {Clubs=teams,Version="pending"};prepared.Add(enriched with {Version="016a:"+Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(enriched,CatalogImportCodec.Json)))});
            }
            var ids=await sync.ImportBatch016AAsync(prepared,ct,async (batch,token)=>{foreach(var link in links)await CatalogIdentityService.MapAsync(batch,link,token);});
            report=report with {Mode="APPLY",Applied=true,SnapshotIds=ids,Status=report.Clubs.All(c=>c.Status=="CURRENT_PROVIDER_REPORTED")?"APPLIED":"APPLIED_INCOMPLETE"};
            row.Applied=true;row.ReportJson=Write(report);await db.SaveChangesAsync(ct);return report;
        } finally {ApplyGate.Release();}
    }
    private static string Normalize(string s)=>string.Concat(s.Normalize(System.Text.NormalizationForm.FormD).Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
