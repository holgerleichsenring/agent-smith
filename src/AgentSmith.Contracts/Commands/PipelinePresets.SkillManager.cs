namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0144: standard Discussion-pipeline shape — Triage picks the skill-manager-*
    // skills (planner, investigator, judge, filter) deterministically (p0143), the
    // proposed SKILL.md goes through GeneratePlan/Approve before AgenticExecute
    // writes it via WriteFile gated by Bootstrap-phase ToolKit (writes restricted
    // to the .agentsmith/ subtree). Pre-p0144's bespoke DiscoverSkills/Evaluate/
    // Approve/Install chain retired — see RetiredCommands for migration hints.
    public static readonly IReadOnlyList<string> SkillManager =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.LoadSkills,
        CommandNames.LoadContext,
        CommandNames.Triage,
        CommandNames.SkillRound,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.GeneratePlan,
        CommandNames.Approval,
        CommandNames.AgenticExecute,
        CommandNames.WriteRunResult,
    ];
}
