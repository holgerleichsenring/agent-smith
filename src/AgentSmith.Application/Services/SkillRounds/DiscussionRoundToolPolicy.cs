using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// Discussion-path tool policy: investigator-mode discussion skills get the
/// read+grep tool surface (verify_hint, survey); pure-prose discussion skills
/// (MAD opinions, legal commentary) get an empty list and run single-shot.
/// Returns empty when no sandbox is active in the pipeline run.
/// </summary>
public sealed class DiscussionRoundToolPolicy(
    IToolKit toolKit, IDecisionLogger decisionLogger) : ISkillRoundToolPolicy
{
    public IReadOnlyList<AITool> GetTools(RoleSkillDefinition role, PipelineContext pipeline)
    {
        if (!IsInvestigatorWithToolAccess(role)) return [];
        if (!pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null) return [];
        var pipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) && pn is not null
            ? pn
            : IToolKit.WildcardPipelineName;
        var hosts = new IToolHost[]
        {
            new FilesystemToolHost(sandbox),
            new LogDecisionToolHost(decisionLogger),
            new HumanToolHost()
        };
        return toolKit.GetToolsFor(
            pipelineName, SkillExecutionPhase.Investigate, role.InvestigatorMode, hosts)
            .ToList();
    }

    private static bool IsInvestigatorWithToolAccess(RoleSkillDefinition role) =>
        string.Equals(role.InvestigatorMode, "verify_hint", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role.InvestigatorMode, "survey", StringComparison.OrdinalIgnoreCase);
}
