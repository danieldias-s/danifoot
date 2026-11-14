using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class Stage7Career
{
    internal static async Task RunAsync(string[] arguments)
    {
        if (arguments.Length != 3 || arguments[2] is not ("invitation" or "dismissal"))
            throw new ArgumentException("stage7-career worldId coachId invitation|dismissal");
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("AUDIT_CAREER_PG"));
        if (connection.Host != "127.0.0.1" || connection.Port != 54983 || connection.Database != "audit_stage7_career")
            throw new ArgumentException("O fixture de carreira usa apenas audit_stage7_career em loopback.");

        await using var services = new ServiceCollection().AddDbContext<ElifootDbContext>(db => db.UseNpgsql(connection.ConnectionString))
            .AddSingleton<IWorldRepository, EfWorldRepository>().BuildServiceProvider();
        var repo = services.GetRequiredService<IWorldRepository>();
        var world = await repo.GetWorldAsync(Guid.Parse(arguments[0])) ?? throw new InvalidOperationException("Liga inexistente.");
        if (!world.Name.StartsWith("Chrome Audit ", StringComparison.Ordinal) || world.RoundState != RoundState.PREPARING)
            throw new InvalidOperationException("O fixture precisa de uma liga sintética em preparação.");
        var coach = world.HumanCoaches.Single(x => x.Id == Guid.Parse(arguments[1]));
        var current = world.Clubs.Single(c => c.Id == coach.CurrentClubId);
        Club target = current;
        if (arguments[2] == "dismissal")
        {
            if (!CoachCareerEngine.Dismiss(world, coach.Id, current, "RED_MORALE_LOSS"))
                throw new InvalidOperationException("O motor não demitiu o treinador.");
        }
        else
        {
            world.RoundNumber = 5;
            coach.LeagueTitles = 1;
            coach.CoachPoints = 70;
            coach.Career.Morale = 85;
            foreach (var ai in world.AiManagers) ai.Career.Morale = 20;
            target = world.Clubs.Where(c => c.HumanCoachId == null && c.DivisionTier == DivisionTier.PrimeiraDivisao)
                .OrderBy(c => c.DeterministicIndex).First();
            target.Reputation = 95;
            target.CashBalance = 12_000_000;
            target.Stadium.BuiltCapacity = 45_000;
            current.Reputation = 30;
            current.CashBalance = 100_000;
            current.Stadium.BuiltCapacity = 10_000;
            CoachCareerEngine.OpenVacancy(world, target);
            CoachCareerEngine.ProcessVacancies(world);
            if (!world.JobInvitations.Any(x => x.CoachId == coach.Id && x.ClubId == target.Id))
                throw new InvalidOperationException("O motor não ofereceu a vaga ao treinador.");
        }
        world.RoundVersion++;
        await repo.SaveWorldAsync(world);
        Console.WriteLine(JsonSerializer.Serialize(new { clubId = target.Id, clubName = target.Name,
            unemployed = coach.IsUnemployed, world.RoundVersion }));
    }
}
