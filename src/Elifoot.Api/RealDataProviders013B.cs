using Elifoot.Infrastructure.Catalog;
using Elifoot.Infrastructure.Catalog.Providers;

namespace Elifoot.Api;

public static class RealDataProviders013B
{
    public static void AddRealDataProviders013B(this WebApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("Catalog013B:Enabled")) return;
        IConfiguration providerConfiguration = builder.Configuration;
        if (builder.Configuration["Catalog013B:ProviderManifest"] is { Length: > 0 } manifest)
            providerConfiguration = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(manifest), optional: false).Build();
        var providers = providerConfiguration.GetSection("Catalog013B:Providers").Get<List<NativeProviderOptions>>() ?? [];
        foreach (var options in providers)
            foreach (var source in NativeProviderFactory.Create(options)) builder.Services.AddSingleton<ICatalogSource>(source);
    }
}
