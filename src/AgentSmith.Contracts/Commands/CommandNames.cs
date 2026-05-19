namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Single source of truth for all pipeline command names. Split into per-subdomain
/// partial files (Pipeline, Security, Api); progress display labels live in the
/// standalone CommandProgressLabels class. Every existing CommandNames.X caller
/// resolves to the same constant regardless of which partial defines it.
///
/// This file holds the retired-command migration table consumed by operator-
/// authored preset validation (e.g. ExtractFindings, BootstrapProjectCommand,
/// LoadCodeMapCommand, and the p0144 skill-manager handler chain).
/// </summary>
public static partial class CommandNames
{
    /// <summary>
    /// Commands that have been retired. Maps the old command name to a migration
    /// hint shown to operators with custom pipeline presets that still reference it.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RetiredCommands =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ExtractFindings"] = "ExtractFindings was retired in p0123. Observations are now produced directly by scanners and gates. Remove the step from your pipeline preset; see .agentsmith/decisions.md p0123 for context.",
            ["BootstrapProjectCommand"] = "BootstrapProject was retired in p0131b. The init-project pipeline now produces .agentsmith/context.yaml + coding-principles.md via a SkillRound dispatch (csharp/node/python/generic-bootstrap); other pipelines should rely on the BootstrapGate added in p0130a to fail-fast on missing files. Remove the step from your custom preset.",
            ["LoadCodeMapCommand"] = "LoadCodeMap was retired in p0131b together with the code-map.yaml artifact. ProjectMap (populated by AnalyzeCode) is now the single source of truth for module/test-project structure; ContextKeys.CodeMap is still emitted as a free-form text rendering for prompt-builders. Remove the step from your custom preset.",
            ["DiscoverSkillsCommand"] = "DiscoverSkillsCommand was retired in p0144. The skill-manager preset now uses the standard SkillRound + Triage flow against the skill-manager-* catalog in agent-smith-skills v2.2.0+. Remove the step from your custom preset; replace with [LoadSkills, Triage, SkillRound, ...] per the standard Discussion-pipeline shape.",
            ["EvaluateSkillsCommand"] = "EvaluateSkillsCommand was retired in p0144. The skill-manager-judge skill now performs the evaluation as a standard SkillRound. Remove the step from your custom preset.",
            ["DraftSkillFilesCommand"] = "DraftSkillFilesCommand was retired in p0144. The skill-manager-planner skill now drafts proposed SKILL.md files as a standard SkillRound, gated by GeneratePlan/Approve. Remove the step from your custom preset.",
            ["ApproveSkillsCommand"] = "ApproveSkillsCommand was retired in p0144. The standard Approve gate (used by fix-bug + add-feature) now handles operator approval of the skill-manager-planner's proposed SKILL.md. Remove the step from your custom preset.",
            ["InstallSkillsCommand"] = "InstallSkillsCommand was retired in p0144. The standard AgenticExecute step now writes the approved SKILL.md to disk via WriteFile gated by Bootstrap-phase ToolKit (writes restricted to the .agentsmith/ subtree). Remove the step from your custom preset.",
        };

    /// <summary>
    /// Returns a migration hint if the given command name was retired in a past phase.
    /// </summary>
    public static bool TryGetRetirementMessage(string commandName, out string message)
    {
        if (RetiredCommands.TryGetValue(commandName, out var found))
        {
            message = found;
            return true;
        }
        message = "";
        return false;
    }
}
