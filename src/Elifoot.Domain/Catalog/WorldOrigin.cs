using Elifoot.Contracts;
using System.Text.Json.Serialization;

namespace Elifoot.Domain;

public partial class GameWorld
{
    [JsonInclude] public FootballGender Gender { get; private set; } = FootballGender.Male;
    [JsonInclude] public WorldCreationOrigin? CatalogOrigin { get; private set; }
    internal void SetImportedCategory(FootballGender gender, WorldCreationOrigin? origin)
    {
        if (CatalogOrigin != null || SeasonNumber != 1 || RoundNumber != 1 || Fixtures.Any(f => f.CurrentMinute > 0))
            throw new InvalidOperationException("Categoria bloqueada.");
        if (origin != null && origin.Gender != gender) throw new ArgumentException("Categoria do pacote difere da origem.");
        Gender = gender;
        if (origin != null) SetCatalogOrigin(origin);
    }
    public void SetCatalogOrigin(WorldCreationOrigin origin)
    {
        if (CatalogOrigin != null || SeasonNumber != 1 || RoundNumber != 1 || Fixtures.Any(f => f.CurrentMinute > 0))
            throw new InvalidOperationException("A origem e a categoria do mundo já foram definidas.");
        Gender = origin.Gender;
        CatalogOrigin = System.Text.Json.JsonSerializer.Deserialize<WorldCreationOrigin>(System.Text.Json.JsonSerializer.Serialize(origin));
    }
}
