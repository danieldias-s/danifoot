using Elifoot.Infrastructure;

namespace Elifoot.Api;

internal static class ProfileAppearanceEndpoints
{
    public static void MapProfileAppearance(this WebApplication app, bool localDev, bool persistent)
    {
        var group = app.MapGroup("/api/profile/appearance");
        group.AddEndpointFilter(async (context, next) =>
        {
            if (!localDev || !persistent)
                return Results.Json(new { error = "Preferência visual indisponível neste perfil da API." }, statusCode: 503);

            // Same LocalDev owner token contract as the existing appearance API.
            var http = context.HttpContext;
            if (!AccountLogin.TryOwner(http, app.Configuration, out var owner))
                return Results.Unauthorized();

            http.Items[typeof(ProfileAppearanceEndpoints)] = owner;
            http.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("", async (HttpContext http, CancellationToken ct) =>
        {
            var db = http.RequestServices.GetRequiredService<ElifootDbContext>();
            return Results.Ok(new AppearanceResponse(await ProfileAppearanceStore.Get(db, Owner(http), ct)));
        });
        group.MapPut("", async (AppearanceResponse request, HttpContext http, CancellationToken ct) =>
        {
            if (request.Mode is not (ProfileAppearanceStore.Current or ProfileAppearanceStore.Classic98))
                return Results.BadRequest(new { error = "Versão visual inválida." });
            var db = http.RequestServices.GetRequiredService<ElifootDbContext>();
            await ProfileAppearanceStore.Save(db, Owner(http), request.Mode, ct);
            return Results.Ok(request);
        });
    }
    private static Guid Owner(HttpContext context) => (Guid)context.Items[typeof(ProfileAppearanceEndpoints)]!;
    private sealed record AppearanceResponse(string Mode);
}
