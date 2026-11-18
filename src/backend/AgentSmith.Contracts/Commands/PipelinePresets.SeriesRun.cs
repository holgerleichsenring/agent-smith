namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-17-0207a: one beat of a series run — the series master's every step of work is a short run with a
    /// run id over the durable series record, so none outlives the wall-time watchdog and any worker takes it.</summary>
    public const string SeriesRunName = "series-run";

    /// <summary>The master runs no skill from the catalog (its body is the server's own) and needs no sandbox.</summary>
    public static readonly IReadOnlyList<string> SeriesRun =
    [
        CommandNames.ReadSeries,
        CommandNames.SeriesMaster,
        CommandNames.PostSeriesStatus,
    ];
}
