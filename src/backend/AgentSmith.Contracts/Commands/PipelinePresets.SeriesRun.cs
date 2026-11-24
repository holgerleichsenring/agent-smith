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
        CommandNames.DeliverSeries, // 2026-11-24-f9c5e
        CommandNames.SeriesMaster,
        CommandNames.PostSeriesStatus,
    ];

    /// <summary>2026-11-17-0207d: the series check once every ticket landed — the default branch checked out, the done
    /// criteria of the series' specs checked by the embedded series-verify-master, and the failures filed as one follow-up
    /// into the series through the filing run's own steps.</summary>
    public const string SeriesVerifyName = "series-verify";

    /// <summary>2026-11-22-9719a: whether a run of this pipeline takes a model place under agent.max_concurrent_runs. The
    /// series' own beats (series-run, series-verify) make short framework calls and do not count: a status update must
    /// not wait behind a coding run that holds the one place. 2026-11-22-6d85e: nor does a conversation turn (spec-dialog) —
    /// a person must not wait behind a coding run for an answer; its tokens still pass the shared rate limiter, and every
    /// run it starts opens its own run scope and takes its own place.</summary>
    public static bool CountsAgainstModelLimit(string pipelineName) =>
        !string.Equals(pipelineName, SeriesRunName, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(pipelineName, SeriesVerifyName, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(pipelineName, SpecDialogName, StringComparison.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<string> SeriesVerify =
    [
        CommandNames.LoadCatalog,
        CommandNames.CheckoutSource,
        CommandNames.VerifySeries,
        CommandNames.PlanFiling,
        CommandNames.FileTickets,
        CommandNames.OpenSeriesRun,
    ];
}
