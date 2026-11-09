namespace Elifoot.Infrastructure.Catalog;

public sealed record HistoricalYearOption(string Year, int ClubCount, List<HistoricalYearCountry> Countries);
public sealed record HistoricalYearCountry(string Code, string Name, int ClubCount);
