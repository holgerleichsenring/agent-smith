using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// p0357: warns when an agent declares a non-default compaction.threshold_iterations.
/// The iteration trigger is a deprecated no-op — compaction fires on token pressure
/// only. The key is still parsed so existing agentsmith.yml files load; operators get
/// one structured warning per affected agent at config load.
/// </summary>
public sealed class CompactionConfigDeprecationWarner(ILogger<CompactionConfigDeprecationWarner> logger)
{
    public void Warn(AgentSmithConfig config)
    {
        foreach (var (name, agent) in config.Agents)
        {
            if (agent.Compaction.ThresholdIterations == CompactionConfig.DefaultThresholdIterations)
                continue;
            logger.LogWarning(
                "Agent '{Agent}' sets deprecated compaction.threshold_iterations={Value}. "
                + "The iteration trigger is a no-op since p0357 — compaction fires on token "
                + "pressure only (max_context_tokens x max_context_tokens_trigger_ratio). "
                + "Remove the key from agentsmith.yml.",
                name, agent.Compaction.ThresholdIterations);
        }
    }
}
