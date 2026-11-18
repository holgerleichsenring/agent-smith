namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>p0179b: the master a pipeline runs at its AgenticMaster step when the
    /// caller named none. Coding is the default because it is the shape every
    /// code-changing preset and every alias resolves to.</summary>
    public const string CodingMaster = "coding-agent-master";

    /// <summary>The master that leads a conversation bound to a ticket a code attempt worked, in place of the
    /// design partner; the turn asks for it and keeps the design partner when the loaded catalog lacks it.</summary>
    public const string TicketStewardMaster = "ticket-steward-master";

    // p0408: the routing table lives NEXT TO the presets, not inside the context
    // builder that used to own it. A run resolves its master here and so does the
    // generated control-flow diagram — one table, so the picture cannot describe a
    // master the run does not load.
    private static readonly Dictionary<string, string> MastersByPipeline =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["security-scan"] = "security-master",
            ["api-security-scan"] = "api-security-master",
            ["legal-analysis"] = "legal-analyst-master",
            ["mad-discussion"] = "mad-discussion-master",
            ["pr-review"] = "pr-review-master", // p0312c
            [SpecDialogName] = "design-partner-master",
            [FileName] = string.Empty, // 2026-11-17-5701b: a filing runs no master
            [CloseName] = string.Empty, // 2026-11-17-e061a: an act on a filed ticket runs no master
            [ReopenName] = string.Empty, [StartName] = string.Empty, [AmendName] = string.Empty, [CommentName] = string.Empty,
            [TicketBodyName] = string.Empty, // 2026-11-17-dd05b
            [RetargetName] = string.Empty, // 2026-11-17-9a39c
            [SeriesRunName] = "series-master", [SeriesVerifyName] = "series-verify-master", // 0207a, 0207d: embedded bodies
        };

    /// <summary>
    /// The master skill an AgenticMaster step loads for this pipeline. Aliases resolve
    /// first, so a configuration naming a retired preset gets the master its canonical
    /// preset runs; anything unmapped is a coding pipeline.
    /// </summary>
    public static string MasterFor(string pipelineName) =>
        MastersByPipeline.GetValueOrDefault(pipelineName, CodingMaster);
}
