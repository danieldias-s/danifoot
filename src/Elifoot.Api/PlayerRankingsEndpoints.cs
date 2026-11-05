using System.Globalization;
using Elifoot.Application;
using Elifoot.Domain;

namespace Elifoot.Api;

public static class PlayerRankingsEndpoints
{
    public static WebApplication MapPlayerRankings(this WebApplication app)
    {
        app.MapGet("/api/worlds/{worldId:guid}/player-rankings", GetRankingsAsync);
        app.MapGet("/api/worlds/{worldId:guid}/players/{playerId:guid}/history", GetHistoryAsync);
        app.MapGet("/api/worlds/{worldId:guid}/season-top-scorers", GetAwardsAsync);
        app.MapGet("/api/worlds/{worldId:guid}/clubs/{clubId:guid}/history", async (
            Guid worldId,Guid clubId,HttpRequest request,PlayerRankingsApplicationService queries,CancellationToken ct)=> {
                if(!TryReadSeason(request,out var season))return Invalid("season deve ser inteiro positivo.");
                return await queries.ReadWorldAsync<IResult>(worldId,w=> {
                    if(season>w.SeasonNumber)return Invalid("Temporada futura não disponível.");
                    var history=HistoryEngine.GetClubHistory(w,clubId,season);
                    return history==null?Results.NotFound():Results.Ok(history);
                },ct)??Results.NotFound();
            });
        return app;
    }

    private static async Task<IResult> GetRankingsAsync(
        Guid worldId, HttpRequest request, PlayerRankingsApplicationService queries, CancellationToken ct)
    {
        if (!TryRead(request, "category", "scorers", out var category) ||
            category is not ("scorers" or "assists" or "goalkeepers" or "defenders"))
            return Invalid("category deve ser scorers, assists, goalkeepers ou defenders.");
        if (!TryRead(request, "scope", "general", out var scope) ||
            scope is not ("general" or "division1" or "division2" or "division3" or "division4" or "cup"))
            return Invalid("scope deve ser general, division1, division2, division3, division4 ou cup.");
        if (!TryRead(request, "period", "career", out var period) ||
            period is not ("career" or "current" or "season"))
            return Invalid("period deve ser career, current ou season.");
        if (!TryReadSeason(request, out var season))
            return Invalid("season deve ser um inteiro positivo.");
        if (period == "season" && season == null)
            return Invalid("Informe season quando period for season.");
        if (period != "season" && season != null)
            return Invalid("season somente pode ser usado com period=season.");

        // ReadWorldAsync must acquire the orchestrator's existing GetLock before loading the world
        // and retain it while constructing the DTO. A separate API/repository lock is insufficient.
        return await queries.ReadWorldAsync<IResult>(worldId, world =>
        {
            if (season > world.SeasonNumber)
                return Invalid("season não pode ser posterior à temporada atual.");
            return Results.Ok(PlayerStatisticsEngine.GetRankings(world, category, scope, period, season));
        }, ct) ?? Results.NotFound();
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid worldId, Guid playerId, PlayerRankingsApplicationService queries, CancellationToken ct)
        => await queries.ReadWorldAsync<IResult>(worldId, world =>
        {
            var history = PlayerStatisticsEngine.GetHistory(world, playerId);
            return history == null ? Results.NotFound() : Results.Ok(history);
        }, ct) ?? Results.NotFound();

    private static async Task<IResult> GetAwardsAsync(
        Guid worldId, HttpRequest request, PlayerRankingsApplicationService queries, CancellationToken ct)
    {
        if (!TryReadSeason(request, out var season))
            return Invalid("season deve ser um inteiro positivo.");

        return await queries.ReadWorldAsync<IResult>(worldId, world =>
        {
            if (season > world.SeasonNumber)
                return Invalid("season não pode ser posterior à temporada atual.");
            // GET only reads already granted awards; closing a season belongs to the domain workflow.
            return Results.Ok(PlayerStatisticsEngine.GetSeasonAwards(world, season));
        }, ct) ?? Results.NotFound();
    }

    private static bool TryRead(HttpRequest request, string key, string fallback, out string value)
    {
        value = fallback;
        if (!request.Query.TryGetValue(key, out var values)) return true;
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0])) return false;
        value = values[0]!.Trim().ToLowerInvariant();
        return true;
    }

    private static bool TryReadSeason(HttpRequest request, out int? season)
    {
        season = null;
        if (!request.Query.TryGetValue("season", out var values)) return true;
        if (values.Count != 1 || !int.TryParse(values[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            return false;
        season = parsed;
        return true;
    }

    private static IResult Invalid(string error) => Results.BadRequest(new { error });
}
