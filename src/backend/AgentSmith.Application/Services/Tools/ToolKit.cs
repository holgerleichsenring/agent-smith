using AgentSmith.Application.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Composes the LLM tool surface for a (pipeline, phase, mode) by
/// intersecting caller-supplied <see cref="IToolHost"/> instances with
/// the allow-list returned by <see cref="IPipelineToolPolicy"/>, then
/// concatenating each active host's per-phase tool list.
///
/// <para>p0177: the IsSubAgent-aware overload threads through to
/// <see cref="IPipelineToolPolicy.GetAllowedHosts(string, bool)"/> so
/// the spawn_agents host stays off the child surface regardless of the
/// pipeline preset.</para>
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
        => GetToolsFor(pipelineName, phase, investigatorMode, hosts, isSubAgent: false);

    public IList<AITool> GetToolsFor(
        string pipelineName,
        SkillExecutionPhase? phase,
        string? investigatorMode,
        IEnumerable<IToolHost> hosts,
        bool isSubAgent)
    {
        ArgumentNullException.ThrowIfNull(pipelineName);
        ArgumentNullException.ThrowIfNull(hosts);
        IReadOnlySet<Type> allowed = _policy.GetAllowedHosts(pipelineName);
        if (isSubAgent)
            allowed = allowed.Where(t => t.Name != "SpawnAgentToolHost").ToHashSet();
        return hosts
            .Where(h => allowed.Contains(h.HostType))
            .SelectMany(h => h.GetTools(phase, investigatorMode))
            .Cast<AITool>()
            .ToList();
    }
}
