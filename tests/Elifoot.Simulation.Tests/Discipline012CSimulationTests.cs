using System.Text.Json;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public void Discipline012C_DirectRed100000DrawsAndExactIntegerWeights()
    {
        int[] weights=[50,30,15,5],boundaries=new int[4],counts=new int[4];
        for(int roll=0;roll<100;roll++)boundaries[DisciplineEngine.DirectRedDuration(roll)-1]++;
        Assert.Equal(weights,boundaries);
        var rng=new DeterministicRng(210031);
        for(int i=0;i<100000;i++)counts[DisciplineEngine.DirectRedDuration(rng.NextInt(0,100))-1]++;
        for(int i=0;i<4;i++)
        {
            double p=weights[i]/100.0,tolerance=6*Math.Sqrt(100000*p*(1-p))+2;
            Assert.InRange((double)counts[i],100000*p-tolerance,100000*p+tolerance);
        }
        Directory.CreateDirectory(DocsDir);
        File.WriteAllText(Path.Combine(DocsDir,"direct-red-100000.json"),JsonSerializer.Serialize(new{Seed=210031,Samples=100000,Weights=weights,Counts=counts,Tolerance="6 binomial standard deviations + 2 counts"},new JsonSerializerOptions{WriteIndented=true}));
    }
}
