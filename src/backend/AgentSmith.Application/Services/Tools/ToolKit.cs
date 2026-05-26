using AgentSmith.Application.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Composes the LLM tool surface for a (pipeline, phase, mode) by
/// intersecting caller-supplied <see cref="IToolHost"/> instances with
/// the allow-list returned by <see cref="IPipelineToolPolicy"/>, then
/// concatenating each active host's per-phase tool list.
/// </summary>
public sealed class ToolKit : IToolKit
{
    private readonly IPipelineToolPolicy _policy;

    public ToolKit(IPipelineToolPolicy policy) => _policy = policy;

    public IList<AITool> GetToolsFor(
        string pipelineName,
        SkillExecutionPhase? phase,
        string? investigatorMode,
        IEnumerable<IToolHost> hosts)
    {
        ArgumentNullException.ThrowIfNull(pipelineName);
        ArgumentNullException.ThrowIfNull(hosts);
        var allowed = _policy.GetAllowedHosts(pipelineName);
        return hosts
            .Where(h => allowed.Contains(h.HostType))
            .SelectMany(h => h.GetTools(phase, investigatorMode))
            .Cast<AITool>()
            .ToList();
    }
}
