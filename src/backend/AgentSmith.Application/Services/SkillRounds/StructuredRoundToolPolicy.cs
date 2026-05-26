using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// Structured-round tool policy: api-security-scan skills get the read+grep
/// surface (probe stays inside the prompt's JSON contract regardless of mode);
/// security-scan triage skills get an empty list — they consume Nuclei / ZAP
/// findings directly and fetching more code would re-investigate the wrong
/// layer. Returns empty when no sandbox is active.
/// </summary>
public sealed class StructuredRoundToolPolicy(
    IToolKit toolKit, IDecisionLogger decisionLogger,
    ILogger<StructuredRoundToolPolicy> logger,
    ILoggerFactory loggerFactory) : ISkillRoundToolPolicy
{
    public IReadOnlyList<AITool> GetTools(RoleSkillDefinition role, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.PipelineName, out var pipelineName) || pipelineName is null)
        {
            logger.LogDebug("tool_policy: role={Role} → no tools (PipelineName missing)", role.Name);
            return [];
        }
        if (!IsApiSecurityPipeline(pipelineName))
        {
            logger.LogDebug(
                "tool_policy: role={Role} pipeline={Pipeline} → no tools (non-api pipeline)",
                role.Name, pipelineName);
            return [];
        }
        if (!pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
        {
            logger.LogWarning(
                "tool_policy: role={Role} pipeline={Pipeline} → no tools (sandbox not in pipeline context)",
                role.Name, pipelineName);
            return [];
        }
        var hosts = new IToolHost[]
        {
            new FilesystemToolHost(sandbox, logger: loggerFactory.CreateLogger<FilesystemToolHost>()),
            new LogDecisionToolHost(decisionLogger)
        };
        var tools = toolKit.GetToolsFor(
            pipelineName, SkillExecutionPhase.Plan, role.InvestigatorMode, hosts)
            .ToList();
        logger.LogInformation(
            "tool_policy: role={Role} pipeline={Pipeline} mode={Mode} → {Count} tools",
            role.Name, pipelineName, role.InvestigatorMode, tools.Count);
        return tools;
    }

    private static bool IsApiSecurityPipeline(string pipelineName) =>
        string.Equals(pipelineName, "api-security-scan", StringComparison.OrdinalIgnoreCase);
}
