namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: a series run's id — random, so any replica mints one without asking another.</summary>
public static class SeriesRunIds
{
    public static string Mint() => "sr-" + Guid.NewGuid().ToString("N")[..12]; // not a limit: the id's width
}
