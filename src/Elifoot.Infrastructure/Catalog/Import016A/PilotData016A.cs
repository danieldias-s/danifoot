using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog.Providers;
namespace Elifoot.Infrastructure.Catalog.Import016A;

public record PilotFetch016A(ImportClubReport016A Report,List<CatalogImportPackage> Packages);
public sealed class PilotData016A(ImportHttp016A http,Import016AOptions options)
{
    private static string Normalize(string s)=>string.Concat(s.Normalize(NormalizationForm.FormD).Where(char.IsLetterOrDigit)).ToUpperInvariant();
    private static bool NameMatches(PilotClub016A club,string name)=>club.Aliases.Append(club.Name).Any(n=>Normalize(n)==Normalize(name));
    private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,CatalogImportCodec.Json)));
    private static DateOnly? Date(string value)=>DateTime.TryParse(value.TrimStart('+'),CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal,out var date)?DateOnly.FromDateTime(date):null;
    private static List<string> Claims(JsonElement entity,string property)=>entity.Child("claims").Child(property).Rows().Where(x=>x.Text("rank")!="deprecated").Select(x=>x.Child("mainsnak").Child("datavalue").Child("value").Text("id")).Where(x=>x.Length>0).ToList();
    private static List<string> Labels(JsonElement entity)=>entity.Child("labels").ValueKind==JsonValueKind.Object?entity.Child("labels").EnumerateObject().Select(p=>p.Value.Text("value")).ToList():[];
    private async Task<(JsonElement Entity,DateTime At)> Entity(string id,bool refresh,CancellationToken ct) {
        if(!System.Text.RegularExpressions.Regex.IsMatch(id,"^Q[1-9][0-9]*$"))throw new ArgumentException("ID Wikidata inválido.");
        var response=await http.Get("wikidata","wiki/Special:EntityData/"+id+".json",refresh,ct);
        var entity=response.Data.Child("entities").Child(id);if(entity.Text("id")!=id||entity.Child("missing").ValueKind!=JsonValueKind.Undefined)throw new ImportProviderException("ENTITY_NOT_FOUND");
        return(entity,response.FetchedAt);
    }
    public async Task<PilotFetch016A> Wikidata(PilotClub016A club,bool refresh,CancellationToken ct,bool includeSquads=true) {
        var(entity,at)=await Entity(club.WikidataId,refresh,ct);var labels=Labels(entity);
        if(!labels.Any(n=>NameMatches(club,n)) && !entity.Child("aliases").EnumerateObject().SelectMany(p=>p.Value.Rows()).Any(a=>NameMatches(club,a.Text("value"))))throw new ImportProviderException("CLUB_NAME_CONFLICT");
        // The women pilot uses a separate, reviewed Wikidata entity; never its parent men's club.
        if(club.Gender==FootballGender.Female && club.WikidataId!="Q522899")throw new ImportProviderException("CATEGORY_NOT_REVIEWED");
        var countries=Claims(entity,"P17");
        if(!countries.Contains(club.CountryEntityId)) {
            var parents=Claims(entity,"P749").Concat(Claims(entity,"P361")).Distinct().ToList();
            if(club.Key!="barcelona-female"||!parents.Contains("Q7156"))throw new ImportProviderException("CLUB_COUNTRY_CONFLICT");
            var(parent,_)=await Entity("Q7156",refresh,ct);if(!Claims(parent,"P17").Contains(club.CountryEntityId))throw new ImportProviderException("CLUB_COUNTRY_CONFLICT");
        }
        var types=Claims(entity,"P31");
        if(!Claims(entity,"P641").Contains("Q2736"))throw new ImportProviderException("WRONG_SPORT");
        if(club.Gender==FootballGender.Female?!types.Contains("Q28140340"):types.Contains("Q28140340")||!types.Any(t=>t is "Q476028" or "Q103229495" or "Q20639856" or "Q10651067" or "Q13580678"))throw new ImportProviderException("CATEGORY_MISMATCH");
        string league=Claims(entity,"P118").FirstOrDefault()??"UNCONFIRMED";
        string season=DateOnly.FromDateTime(at).Year.ToString(); // Wikidata query date, NOT a claim of league-season availability.
        string leagueName="Liga não confirmada para a temporada";
        var issues=new List<string>{"Wikidata não confirma cobertura completa nem participação na temporada atual; datas de vínculos são preservadas."};
        string evidence=$"https://www.wikidata.org/wiki/{club.WikidataId}?oldid={entity.Text("lastrevid")}";
        var team=new CatalogTeam(club.WikidataId,club.Name,club.Name[..Math.Min(5,club.Name.Length)],league,leagueName,
            Verification:new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,evidence,at,CurrentSeasonConfirmed:false,WikidataId:club.WikidataId));
        PilotFetch016A ClubOnly(string issue,int received=0) {
            var notes=issues.Append(issue).ToList();
            var metadata=Packages(club,team,[],"wikidata",season,at,CatalogRightsPolicy.WikidataRights,notes,0).Where(p=>p.Scope.DataType==CatalogDataType.Clubs).ToList();
            return new(new(club.Key,club.Name,club.CountryCode,club.Gender,"wikidata",club.WikidataId,club.WikidataId,season,at,"INCOMPLETE",received,0,[],0,0,0,0,notes,[],[]),metadata);
        }
        if(!includeSquads)return ClubOnly("SQUADS_NOT_REQUESTED: identificação do clube somente.");
        string query="SELECT DISTINCT ?player ?playerLabel ?positionLabel ?membership ?start ?end ?sex ?referenceUrl ?retrieved WHERE { ?player p:P54 ?membership . ?membership ps:P54 wd:"+club.WikidataId+" . OPTIONAL { ?membership pq:P580 ?start } OPTIONAL { ?membership pq:P582 ?end } OPTIONAL { ?player wdt:P21 ?sex } OPTIONAL { ?membership prov:wasDerivedFrom ?reference . ?reference pr:P854 ?referenceUrl . OPTIONAL { ?reference pr:P813 ?retrieved } } OPTIONAL { ?player wdt:P413 ?position } SERVICE wikibase:label { bd:serviceParam wikibase:language \"en,pt\". } } ORDER BY ?player ?membership LIMIT 500";
        ImportResponse016A response;
        try {response=await http.Get("wikidata","sparql?format=json&query="+Uri.EscapeDataString(query),refresh,ct);}
        catch(ImportProviderException e){return ClubOnly("SQUAD_UNAVAILABLE:"+e.Code); }
        var rows=response.Data.Child("results").Child("bindings");if(rows.ValueKind!=JsonValueKind.Array)return ClubOnly("INVALID_SPARQL_RESPONSE");
        if(rows.GetArrayLength()>=500)return ClubOnly("SPARQL_TRUNCATED",rows.GetArrayLength());
        var players=new List<CatalogAthlete>();var reportPlayers=new List<ImportPlayer016A>();int duplicate=0;
        foreach(var member in rows.Rows().GroupBy(r=>r.Child("player").Text("value"))) {
            string id=member.Key.Split('/').Last(),name=member.First().Child("playerLabel").Text("value");
            var distinct=member.DistinctBy(r=>r.GetRawText()).ToList();duplicate+=member.Count()-distinct.Count;
            var records=distinct.GroupBy(r=>r.Child("membership").Text("value")).ToList();
            // Prefer a dated active statement, otherwise retain the latest historical/open statement for audit.
            var selected=records.OrderByDescending(g=>CatalogMembershipPolicy.Classify(Date(g.First().Child("start").Text("value")),Date(g.First().Child("end").Text("value")),DateOnly.FromDateTime(response.FetchedAt))==CatalogMembershipStatus.DatedActive)
                .ThenByDescending(g=>Date(g.First().Child("start").Text("value"))).First();
            var start=Date(selected.First().Child("start").Text("value"));var end=Date(selected.First().Child("end").Text("value"));var asOf=DateOnly.FromDateTime(response.FetchedAt);
            var status=CatalogMembershipPolicy.Classify(start,end,asOf);
            bool freshReference=selected.Any(r=>Uri.TryCreate(r.Child("referenceUrl").Text("value"),UriKind.Absolute,out var u)&&u.Scheme=="https"&&Date(r.Child("retrieved").Text("value")) is DateOnly retrieved&&retrieved<=asOf&&retrieved>=asOf.AddDays(-30));
            if(status==CatalogMembershipStatus.DatedActive&&!freshReference)status=CatalogMembershipStatus.Unknown;
            var sexes=selected.Select(r=>r.Child("sex").Text("value").Split('/').Last()).ToHashSet();
            string[] expected=club.Gender==FootballGender.Female?["Q6581072","Q1052281"]:["Q6581097","Q2449503"];
            string[] opposite=club.Gender==FootballGender.Female?["Q6581097","Q2449503"]:["Q6581072","Q1052281"];
            bool categoryConfirmed=sexes.Overlaps(expected)&&!sexes.Overlaps(opposite);
            var mapped=new HashSet<string>();var rawPositions=selected.Select(r=>r.Child("positionLabel").Text("value")).Distinct().ToList();
            foreach(string original in rawPositions){try{mapped.Add(CatalogImportCodec.Position(original));}catch(ArgumentException){}}
            string? position=mapped.Count==1?mapped.Single():null;var missing=new List<string>();if(!categoryConfirmed)missing.Add("CATEGORY_UNCONFIRMED");if(position==null)missing.Add("POSITION_MISSING_OR_AMBIGUOUS");if(name==id||string.IsNullOrWhiteSpace(name))missing.Add("NAME_MISSING");
            var origin=status==CatalogMembershipStatus.DatedActive?WorldPlayerOrigin.REAL_CURRENT_VERIFIED:WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE;
            reportPlayers.Add(new(id,name,position,string.Join(" / ",rawPositions),origin,club.WikidataId,season,response.FetchedAt,selected.Key,missing));
            if(missing.Count>0){issues.Add(id+": "+string.Join(",",missing));continue;}
            players.Add(new(id,club.WikidataId,"wikidata",club.Gender,season,name,position!,25,"wikidata",response.FetchedAt,"wikidata:"+id,options.FixtureData?CatalogDataOrigin.Fixture:CatalogDataOrigin.Real,
                new(InitialPlayerStrength.Version+":pending",CatalogDataOrigin.Estimated,.2,Hash(new{id,position}),.5,null),MembershipStart:start,MembershipEnd:end,OriginalPosition:string.Join(" / ",rawPositions),
                MembershipEvidence:new(status,selected.Key,evidence,asOf,"Uma associação aberta ou histórica não prova vínculo atual."),PlayerOrigin:origin));
        }
        int current=players.Count(p=>p.PlayerOrigin==WorldPlayerOrigin.REAL_CURRENT_VERIFIED);
        var packages=Packages(club,team,players,"wikidata",season,response.FetchedAt,CatalogRightsPolicy.WikidataRights,issues,rows.GetArrayLength());
        return new(new(club.Key,club.Name,club.CountryCode,club.Gender,"wikidata",club.WikidataId,club.WikidataId,season,response.FetchedAt,"INCOMPLETE",rows.GetArrayLength(),current,
            players.GroupBy(p=>p.Position).ToDictionary(g=>g.Key,g=>g.Count()),duplicate,0,current,reportPlayers.Count(p=>p.Origin==WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE),issues,[],reportPlayers),packages);
    }
    public async Task<PilotFetch016A> ApiFootball(PilotClub016A club,Import016AMapping? mapping,bool refresh,CancellationToken ct,bool includeSquads=true) {
        if(!http.HasApiKey)throw new ImportProviderException("NOT_CONNECTED");
        if(mapping==null) {
            var search=await http.Get("api-football","teams?search="+Uri.EscapeDataString(club.Key=="barcelona-female"?"Barcelona":club.Name),refresh,ct);
            var candidates=search.Data.Child("response").Rows().Select(r=>new ImportCandidate016A(r.Child("team").Text("id"),r.Child("team").Text("name"),r.Child("team").Text("country"),null,null,true)).ToList();
            return new(new(club.Key,club.Name,club.CountryCode,club.Gender,"api-football",null,club.WikidataId,"UNCONFIRMED",search.FetchedAt,"IDENTITY_REVIEW_REQUIRED",0,0,[],0,0,0,0,["Confirme país, categoria e liga por IDs; nenhum nome selecionado automaticamente."],candidates,[]),[]);
        }
        var teamResponse=await http.Get("api-football","teams?id="+mapping.TeamId,refresh,ct);var teams=teamResponse.Data.Child("response").Rows();
        if(teams.Count!=1||teams[0].Child("team").Text("id")!=mapping.TeamId||!NameMatches(club,teams[0].Child("team").Text("name"))||teams[0].Child("team").Child("national").ValueKind!=JsonValueKind.False)throw new ImportProviderException("TEAM_IDENTITY_MISMATCH");
        string country=teams[0].Child("team").Text("country");string expected=club.CountryCode switch{"BR"=>"Brazil","ES"=>"Spain","PT"=>"Portugal",_=>""};if(country!=expected)throw new ImportProviderException("TEAM_COUNTRY_MISMATCH");
        var leagues=await http.Get("api-football","leagues?team="+mapping.TeamId+"&current=true",refresh,ct);
        var league=leagues.Data.Child("response").Rows().SingleOrDefault(r=>r.Child("league").Text("id")==mapping.LeagueId);
        if(league.ValueKind==JsonValueKind.Undefined||league.Child("league").Text("name")!=mapping.LeagueName||league.Child("country").Text("name")!=expected)throw new ImportProviderException("LEAGUE_CATEGORY_NOT_CONFIRMED");
        var now=DateOnly.FromDateTime(DateTime.UtcNow);var seasons=league.Child("seasons").Rows().Where(s=>s.Child("current").ValueKind==JsonValueKind.True&&Date(s.Text("start"))<=now&&Date(s.Text("end"))>=now).ToList();
        if(seasons.Count!=1)throw new ImportProviderException("CURRENT_SEASON_NOT_AVAILABLE");string year=seasons[0].Text("year");
        string season=year; // Provider year is discovered from start/end, never assumed for every league.
        if(!includeSquads) {
            var metadataTeam=new CatalogTeam(mapping.TeamId,club.Name,club.Name[..Math.Min(5,club.Name.Length)],mapping.LeagueId,mapping.LeagueName,
                Verification:new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,mapping.Evidence,teamResponse.FetchedAt,season,true,WikidataId:club.WikidataId));
            var notes=new List<string>{"SQUADS_NOT_REQUESTED: identificação do clube somente."};
            var metadata=Packages(club,metadataTeam,[],"api-football",season,teamResponse.FetchedAt,options.ApiFootballRights!,notes,0).Where(p=>p.Scope.DataType==CatalogDataType.Clubs).ToList();
            return new(new(club.Key,club.Name,club.CountryCode,club.Gender,"api-football",mapping.TeamId,club.WikidataId,season,teamResponse.FetchedAt,"INCOMPLETE",0,0,[],0,0,0,0,notes,[],[]),metadata);
        }
        var squadResponse=await http.Get("api-football","players/squads?team="+mapping.TeamId,refresh,ct);var squads=squadResponse.Data.Child("response").Rows();
        if(squads.Count!=1||squads[0].Child("team").Text("id")!=mapping.TeamId)throw new ImportProviderException("SQUAD_TEAM_MISMATCH");
        var raw=squads[0].Child("players").Rows();if(raw.Count==0)throw new ImportProviderException("EMPTY_SQUAD");
        var players=new List<CatalogAthlete>();var reportPlayers=new List<ImportPlayer016A>();var issues=new List<string>();int duplicate=0;
        foreach(var group in raw.GroupBy(p=>p.Text("id"))) {
            if(string.IsNullOrWhiteSpace(group.Key)||group.Any(p=>p.GetRawText()!=group.First().GetRawText())){issues.Add("CONFLICTING_PLAYER_ID:"+group.Key);continue;}
            duplicate+=group.Count()-1;var person=group.First();string name=person.Text("name"),original=person.Text("position");string? position=null;var missing=new List<string>();
            try{position=CatalogImportCodec.Position(original);}catch(ArgumentException){missing.Add("POSITION_MISSING_OR_AMBIGUOUS");}
            if(string.IsNullOrWhiteSpace(name))missing.Add("NAME_MISSING");
            string evidence="https://v3.football.api-sports.io/players/squads?team="+mapping.TeamId;
            reportPlayers.Add(new(group.Key,name,position,original,WorldPlayerOrigin.REAL_CURRENT_PROVIDER_REPORTED,mapping.TeamId,season,squadResponse.FetchedAt,evidence,missing));
            if(missing.Count>0){issues.Add(group.Key+":"+string.Join(',',missing));continue;}
            players.Add(new(group.Key,mapping.TeamId,"api-football",club.Gender,season,name,position!,25,"api-football",squadResponse.FetchedAt,"api-football:"+group.Key,options.FixtureData?CatalogDataOrigin.Fixture:CatalogDataOrigin.Real,
                new(InitialPlayerStrength.Version+":pending",CatalogDataOrigin.Estimated,.2,Hash(new{group.Key,position}),.5,null),OriginalPosition:original,
                MembershipEvidence:new(CatalogMembershipStatus.ProviderReported,"api-football:team:"+mapping.TeamId+":player:"+group.Key,evidence,DateOnly.FromDateTime(squadResponse.FetchedAt),"Elenco atual reportado pelo fornecedor; sem verificação independente."),PlayerOrigin:WorldPlayerOrigin.REAL_CURRENT_PROVIDER_REPORTED));
        }
        var team=new CatalogTeam(mapping.TeamId,club.Name,club.Name[..Math.Min(5,club.Name.Length)],mapping.LeagueId,mapping.LeagueName,
            Verification:new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,mapping.Evidence,teamResponse.FetchedAt,season,true,WikidataId:club.WikidataId));
        var rights=options.ApiFootballRights??throw new ImportProviderException("RIGHTS_NOT_CONFIRMED");
        var packages=Packages(club,team,players,"api-football",season,squadResponse.FetchedAt,rights,issues,raw.Count);
        bool complete=PlayableWorldPolicy.Complete(players);return new(new(club.Key,club.Name,club.CountryCode,club.Gender,"api-football",mapping.TeamId,club.WikidataId,season,squadResponse.FetchedAt,complete?"CURRENT_PROVIDER_REPORTED":"INCOMPLETE",raw.Count,players.Count,
            players.GroupBy(p=>p.Position).ToDictionary(g=>g.Key,g=>g.Count()),duplicate,players.Count,0,0,issues,[],reportPlayers),packages);
    }
    private List<CatalogImportPackage> Packages(PilotClub016A club,CatalogTeam team,List<CatalogAthlete> players,string provider,string season,DateTime at,CatalogUsageRights rights,List<string> issues,int received) {
        var clubScope=new CatalogScopeKey(club.CountryCode,club.Gender,"016a:"+club.Key,season,CatalogDataType.Clubs,provider);
        var clubs=new CatalogImportPackage(clubScope,club.CountryName,"pending",at,true,1,1,1,rights.StorageAllowed,rights.CommercialUseAllowed,rights.Evidence,options.FixtureData,[team],[],[],Rights:rights);
        var squads=clubs with {Scope=clubScope with {DataType=CatalogDataType.Squads},Clubs=[],Players=players,ExpectedItems=players.Count,AllowPartialSquads=true,AllowCountDecrease=provider=="api-football"&&PlayableWorldPolicy.Complete(players),
            RosterAsOf=DateOnly.FromDateTime(at),RosterReports=[new(team.ExternalId,provider,received,players.Count,PlayableWorldPolicy.Complete(players),issues.ToList())]};
        return [clubs with {Version="016a:"+Hash(clubs)},squads with {Version="016a:"+Hash(squads)}];
    }
}
