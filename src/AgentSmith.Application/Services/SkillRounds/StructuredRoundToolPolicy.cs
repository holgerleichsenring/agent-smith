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
/// Structured-round tool policy: api-security-scan skills get the read+grep
/// surface (probe stays inside the prompt's JSON contract regardless of mode);
/// security-scan triage skills get an empty list — they consume Nuclei / ZAP
/// findings directly and fetching more code would re-investigate the wrong
/// layer. Returns empty when no sandbox is active.
/// </summary>
public sealed class StructuredRoundToolPolicy(
    IToolKit toolKit, IDecisionLogger decisionLogger) : ISkillRoundToolPolicy
{
    public IReadOnlyList<AITool> GetTools(RoleSkillDefinition role, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.PipelineName, out var pipelineName) || pipelineName is null) return [];
        if (!IsApiSecurityPipeline(pipelineName)) return [];
        if (!pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null) return [];
        var hosts = new IToolHost[]
        {
            new FilesystemToolHost(sandbox),
            new LogDecisionToolHost(decisionLogger)
        };
        return toolKit.GetToolsFor(
            pipelineName, SkillExecutionPhase.Plan, role.InvestigatorMode, hosts)
            .ToList();
    }

    private static bool IsApiSecurityPipeline(string pipelineName) =>
        string.Equals(pipelineName, "api-security-scan", StringComparison.OrdinalIgnoreCase);
}
