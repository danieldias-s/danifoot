using Elifoot.Infrastructure.Catalog.Providers;

namespace Elifoot.Integration.Tests;

public sealed class OpenFootball013BTests
{
    private static readonly DateTime Reference = new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
    private static string Document(string rows) => "{\"name\":\"Synthetic test competition\",\"matches\":[" + rows + "]}";
    [Fact]
    public void PlayedResultsAreDeduplicatedAndFutureOrUnplayedFixturesDoNotAffectElo()
    {
        const string played = "{\"date\":\"2026-10-20\",\"team1\":\"a\",\"team2\":\"b\",\"score\":{\"ft\":[2,1]}}";
        var result = OpenFootballResultParser.Parse(Document(played + "," + played + ",{\"date\":\"2026-11-01\",\"team1\":\"a\",\"team2\":\"b\",\"score\":{\"ft\":[9,0]}},{\"date\":\"2026-10-21\",\"team1\":\"a\",\"team2\":\"c\"}"), Reference, s => s);
        var match = Assert.Single(result.Results); Assert.Equal(2, match.HomeGoals); Assert.Equal(2, result.FutureOrUnplayed); Assert.Equal(3, result.Teams.Count);
        Assert.Equal(DateTimeKind.Utc, match.PlayedAt.Kind);
    }
    [Theory]
    [InlineData("{\"date\":\"2026-10-01\",\"team1\":\"a\",\"team2\":\"a\",\"score\":{\"ft\":[1,0]}}")]
    [InlineData("{\"date\":\"invalid\",\"team1\":\"a\",\"team2\":\"b\",\"score\":{\"ft\":[1,0]}}")]
    [InlineData("{\"date\":\"2026-10-01\",\"team1\":\"a\",\"team2\":\"b\",\"score\":{\"ft\":[-1,0]}}")]
    [InlineData("{\"date\":\"2026-10-01\",\"team1\":\"a\",\"team2\":\"b\",\"score\":{\"ft\":[1]}}")]
    public void InvalidSourceEvidenceIsRejected(string row) => Assert.Throws<ArgumentException>(() => OpenFootballResultParser.Parse(Document(row), Reference, s => s));
    [Fact]
    public void ConflictingScoresForSameFixtureAreRejected()
    {
        string Row(int score) => "{\"date\":\"2026-10-01\",\"team1\":\"a\",\"team2\":\"b\",\"score\":{\"ft\":[" + score + ",0]}}";
        Assert.Throws<ArgumentException>(() => OpenFootballResultParser.Parse(Document(Row(1) + "," + Row(2)), Reference, s => s));
        Assert.Throws<ArgumentException>(() => OpenFootballResultParser.Parse("{}", Reference, s => s));
    }
}
