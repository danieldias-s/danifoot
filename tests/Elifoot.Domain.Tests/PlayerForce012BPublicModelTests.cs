using System.Text.Json;

namespace Elifoot.Domain.Tests;

public class PlayerForce012BPublicModelTests
{
    [Fact]
    public void PublishedPreAndPostBoundaryConditionsAreNotEquivalent()
    {
        // Independently expressed predicates for comparing two PUBLIC descriptions, never a production rule.
        // This does not test ELIFOOT.EXE or supply its missing performance evaluator/base.
        var differences = new List<object>();
        int comparisons = 0;
        for (int strength = 1; strength <= 50; strength++)
        for (int basis = 1; basis <= 50; basis++)
        foreach (int assessment in new[] { -1, 0, 1 })
        foreach (bool goalkeeper in new[] { false, true })
        foreach (bool gatePassed in new[] { false, true })
        {
            bool gain = assessment == 1 && strength < 50 && (!goalkeeper || gatePassed);
            bool loss = assessment == -1 && strength > 1 && gatePassed;
            int beforeComparison = strength
                + (gain && strength <= basis + 5 ? 1 : 0)
                - (loss && strength >= basis - 5 ? 1 : 0);
            int afterComparison = strength
                + (gain && strength + 1 <= basis + 5 ? 1 : 0)
                - (loss && strength - 1 >= basis - 5 ? 1 : 0);
            comparisons++;
            if (beforeComparison != afterComparison)
            {
                Assert.True((assessment == 1 && strength == basis + 5)
                    || (assessment == -1 && strength == basis - 5));
                differences.Add(new { Strength = strength, Base = basis, Assessment = assessment,
                    Goalkeeper = goalkeeper, GatePassed = gatePassed, ReadmeResult = beforeComparison,
                    PublicCResult = afterComparison, OriginalResult = (int?)null });
            }
        }
        Assert.Equal(30_000, comparisons);
        Assert.Equal(220, differences.Count);
        string directory = Path.Combine(Environment.GetEnvironmentVariable("ELIFOOT_AUDIT_OUTPUT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/audit-012b")), "force-012b");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "published-model-counterexamples.json"),
            JsonSerializer.Serialize(new { Comparisons = comparisons, Counterexamples = differences,
                Scope = "PUBLIC_DESCRIPTION_COMPARISON", OriginalExecuted = false }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
