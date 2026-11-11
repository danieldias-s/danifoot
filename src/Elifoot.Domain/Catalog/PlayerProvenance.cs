using System.Text.Json.Serialization;
using Elifoot.Contracts;
namespace Elifoot.Domain;

public partial class Player
{
    public WorldPlayerProvenance? CatalogIdentity { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PersonalModifierB { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PersonalModifierD { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nationality { get; set; }
}
