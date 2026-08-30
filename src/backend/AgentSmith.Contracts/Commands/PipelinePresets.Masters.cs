namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>p0179b: the master a pipeline runs at its AgenticMaster step when the
    /// caller named none. Coding is the default because it is the shape every
    /// code-changing preset and every alias resolves to.</summary>
    public const string CodingMaster = "coding-agent-master";

    /// <summary>2026-08-30-18e3: the one master asked to state an entry map. Three masters
    /// declare the observation schema; the other two keep the shape they have — an api scan
    /// runs its source checkout fail-soft and often holds no source, and a pr review sees a
    /// diff rather than a system.</summary>
    public const string SecurityMaster = "security-master";

    // p0408: the routing table lives NEXT TO the presets, not inside the context
    // builder that used to own it. A run resolves its master here and so does the
    // generated control-flow diagram — one table, so the picture cannot describe a
    // master the run does not load.
    private static readonly Dictionary<string, string> MastersByPipeline =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["security-scan"] = SecurityMaster,
            ["api-security-scan"] = "api-security-master",
            ["legal-analysis"] = "legal-analyst-master",
            ["mad-discussion"] = "mad-discussion-master",
            ["pr-review"] = "pr-review-master", // p0312c
            [SpecDialogName] = "design-partner-master",
        };

    /// <summary>
    /// The master skill an AgenticMaster step loads for this pipeline. Aliases resolve
    /// first, so a configuration naming a retired preset gets the master its canonical
    /// preset runs; anything unmapped is a coding pipeline.
    /// </summary>
    public static string MasterFor(string pipelineName) =>
        MastersByPipeline.GetValueOrDefault(Canonical(pipelineName), CodingMaster);
}
