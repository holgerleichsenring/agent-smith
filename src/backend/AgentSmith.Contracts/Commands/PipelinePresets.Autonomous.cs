namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0144: standard Discussion-pipeline shape with autonomous-* skills (planner,
    // investigator, judge, filter). Adds SkillRound between Triage and Convergence
    // (pre-p0144 the preset had Triage but no SkillRound — crashed since p0131c).
    // WriteTickets persists the autonomous-* findings as new issues/MR drafts before
    // the run-result write closes the run.
    public static readonly IReadOnlyList<string> Autonomous =
    [
        CommandNames.LoadCatalog,
        CommandNames.PipelineNameInitializer,
        CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadContext,
        CommandNames.LoadRuns,
        CommandNames.LoadSkills,
        CommandNames.Triage,
        CommandNames.SkillRound,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.WriteTickets,
        CommandNames.WriteRunResult,
    ];
}
