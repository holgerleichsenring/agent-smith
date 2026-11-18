namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-17-5701b: preset name of the filing run — the one path an approval files tickets through.</summary>
    public const string FileName = "file";

    // 2026-11-17-5701b: creating and starting are one step, so no cancel between steps leaves a created root
    // unstarted. Launched by the server with a FilingRequest and a FilingReport slot; no catalog, no sandbox, no
    // master — a filing reads nothing a skill would need.
    public static readonly IReadOnlyList<string> File =
    [
        CommandNames.PlanFiling,
        CommandNames.FileTickets,
        CommandNames.OpenSeriesRun, // 2026-11-17-0207a: two or more filed tickets are a series the series master owns
    ];
}
