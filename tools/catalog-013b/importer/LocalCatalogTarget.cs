using Npgsql;
namespace Elifoot.RealDataImporter;

// Explicit DEV targets; the integration never falls back to an original database.
internal static class LocalCatalogTarget
{
    internal static bool Catalog(NpgsqlConnectionStringBuilder connection) => connection.Host == "127.0.0.1" &&
        (connection.Port == 55434 && connection.Database == "elifoot_013b_catalog" ||
         connection.Port == 55435 && connection.Database == "elifoot_014a_013b_catalog_real" ||
         connection.Port == 55436 && connection.Database == "elifoot_013c_013b_catalog_real" ||
         connection.Port == 55437 && connection.Database == "elifoot_014b_013b_catalog_real");
    internal static bool Worlds(NpgsqlConnectionStringBuilder catalog, NpgsqlConnectionStringBuilder world) =>
        Catalog(catalog) && world.Host == "127.0.0.1" && world.Port == catalog.Port &&
        world.Database == (catalog.Port == 55437 ? "elifoot_014b_013b_worlds_real" : catalog.Port == 55436 ? "elifoot_013c_013b_worlds_real" : catalog.Port == 55435 ? "elifoot_014a_013b_worlds_real" : "elifoot_013b_worlds");
}
