using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Infrastructure.Catalog.Providers;

/// <summary>Reads exact reviewed QIDs, P17 country and P6364/P462 color items with P465 RGB. No name search or images.</summary>
public sealed class WikidataClubColorSource(ProviderHttp http):IClubColorSource
{
    private static IEnumerable<JsonElement> Statements(JsonElement entity,string property)=>entity.TryGetProperty("claims",out var claims)&&claims.TryGetProperty(property,out var values)?values.EnumerateArray().Where(c=>!c.TryGetProperty("rank",out var rank)||rank.GetString()!="deprecated"):[];
    private static JsonElement Value(JsonElement claim)=>claim.GetProperty("mainsnak").TryGetProperty("datavalue",out var value)?value.GetProperty("value"):default;
    private static string? Item(JsonElement claim){var value=Value(claim);return value.ValueKind==JsonValueKind.Object&&value.TryGetProperty("id",out var id)?id.GetString():null;}
    public async Task<ClubColorPalette?> FindAsync(EligibleCatalogClub club,CancellationToken ct=default)
    {
        var qid=club.Verification?.WikidataId??club.Aliases?.FirstOrDefault(a=>a.Provider=="wikidata")?.ExternalId;
        if(qid==null||!System.Text.RegularExpressions.Regex.IsMatch(qid,"^Q[1-9][0-9]*$"))return null;
        var budget=new ProviderRequestBudget(3);
        async Task<JsonElement> Fetch(string ids)=>await http.Get("w/api.php?action=wbgetentities&ids="+Uri.EscapeDataString(ids)+"&props=claims%7Cinfo&format=json&maxlag=5",budget,ct);
        var root=await Fetch(qid);if(!root.TryGetProperty("entities",out var entities)||!entities.TryGetProperty(qid,out var entity))throw new ArgumentException("Resposta Wikidata incompleta.");
        var countryIds=club.CountryCode switch {"BR"=>new[]{"Q155"},"AR"=>["Q414"],"GB"=>["Q145","Q21","Q22","Q25","Q26"],"ES"=>["Q29"],"DE"=>["Q183"],"IT"=>["Q38"],"FR"=>["Q142"],"PT"=>["Q45"],_=>[]};
        if(!Statements(entity,"P17").Select(Item).Any(id=>countryIds.Contains(id)))throw new ArgumentException("País Wikidata diverge ou está ausente; revisão necessária.");
        var colors=Statements(entity,"P6364").ToList();string property="P6364";
        if(colors.Count==0){colors=Statements(entity,"P462").ToList();property="P462";}
        var ids=colors.Select(Item).Where(id=>id!=null).Distinct().Take(8).Cast<string>().ToList();if(ids.Count==0)return null;
        var colorRoot=await Fetch(string.Join('|',ids));var rgb=new List<string>();
        foreach(var id in ids){
            if(!colorRoot.GetProperty("entities").TryGetProperty(id,out var color))continue;
            foreach(var claim in Statements(color,"P465")){
                var value=Value(claim);if(value.ValueKind!=JsonValueKind.String)continue;
                var hex="#"+value.GetString();if(ClubColorPolicy.Hex(hex)){rgb.Add(hex.ToUpperInvariant());break;}
            }
        }
        rgb=rgb.Distinct().ToList();if(rgb.Count==0)return null;
        return new(rgb[0],rgb.Count>1?rgb[1]:null,null,"Wikidata CC0",true,DateTime.UtcNow,property=="P6364"?.9:.75,
            "wikidata:"+qid+":"+entity.GetProperty("lastrevid").ToString(),club.CountryCode,club.Gender,club.Provider,club.ExternalId,
            $"https://www.wikidata.org/wiki/{qid}; reviewed catalog QID and country; {property} color entities {string.Join(',',ids)}; P465 representative RGB; extra colors: {string.Join(',',rgb.Skip(2))}");
    }
}
public sealed class SavedClubColorSource(string file):IClubColorSource
{
    public Task<ClubColorPalette?> FindAsync(EligibleCatalogClub club,CancellationToken ct=default)
    {
        if(!File.Exists(file))return Task.FromResult<ClubColorPalette?>(null);
        var rows=JsonSerializer.Deserialize<List<ImportClubColorRequest>>(File.ReadAllText(file),CatalogImportCodec.Json)??[];
        return Task.FromResult(rows.FirstOrDefault(r=>r.CountryCode==club.CountryCode&&r.Gender==club.Gender&&r.Provider==club.Provider&&r.ExternalId==club.ExternalId)?.Palette);
    }
}
