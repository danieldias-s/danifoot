using Elifoot.Contracts;
using System.Text.Json.Serialization;

namespace Elifoot.Domain;

public partial class GameWorld
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OfflineWorldOrigin? OfflineCatalogOrigin { get; set; }
}
public partial class Club
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OfflineClubIdentity? OfflineIdentity { get; set; }
}
public partial class Player
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OfflinePlayerIdentity? OfflineIdentity { get; set; }
}
public partial class AiManager
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OfflineCoachIdentity? OfflineIdentity { get; set; }
}
