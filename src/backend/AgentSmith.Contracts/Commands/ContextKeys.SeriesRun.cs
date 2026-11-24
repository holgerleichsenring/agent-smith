namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-0207a: the keys a series run's beat and verify carry.</summary>
public static partial class ContextKeys
{
    /// <summary>The series run's id (string): seeded on a beat or a verify, and on a filing that joins a series.</summary>
    public const string SeriesRunId = "SeriesRunId";

    /// <summary>2026-11-24-f9c5b: the id of the series a filing opened before starting its tickets (string).</summary>
    public const string SeriesRunOpened = "SeriesRunOpened";

    /// <summary>The beat being run (Application's SeriesBeat), set by ReadSeries.</summary>
    public const string SeriesBeat = "SeriesBeat";
}
