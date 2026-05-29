namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0179e: collapsed shape. Triage / ConvergenceCheck / CompileDiscussion /
    // LoadSkills retired FROM THIS PRESET. AgenticMaster loads
    // mad-discussion-master, which internally orchestrates the five
    // perspectives (dreamer, realist, philosopher, devils-advocate, silencer)
    // via spawn_agents and synthesises the result. CompileDiscussionHandler
    // stays on disk until skill-manager / autonomous also migrate off the
    // choreography (final cleanup slice).
    public static readonly IReadOnlyList<string> MadDiscussion =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.LoadContext,
        CommandNames.AgenticMaster,         // p0179e: loads mad-discussion-master
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
