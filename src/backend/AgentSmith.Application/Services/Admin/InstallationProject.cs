using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: the project a run on the installation executes against. It is in no catalog: it carries the
/// installation's label, one synthetic repository that never exists (the runs read the server's own records, not a
/// checkout), and the admin agent for the admin assistant's turn. The master-less installation runs take a default
/// agent, because they call no model.
/// </summary>
public static class InstallationProject
{
    public static ResolvedProject Build(AgentSmithConfig config, string pipelineName)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new ResolvedProject
        {
            Name = InstallationScope.Name,
            Agent = string.Equals(pipelineName, PipelinePresets.AdminTurnName, StringComparison.OrdinalIgnoreCase)
                ? AdminAgent(config) : new AgentConfig(),
            Repos = [new RepoConnection { Name = "installation", Type = RepoType.Local, Path = EphemeralSource.NoSourcePath }],
        };
    }

    /// <summary>The agent dialogue.admin_agent names; a refusal that names the key when it names none or an unknown one.</summary>
    public static AgentConfig AdminAgent(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var name = config.Dialogue.AdminAgent;
        if (!string.IsNullOrWhiteSpace(name) && config.Agents.TryGetValue(name, out var agent)) return agent;
        throw new ConfigurationException(string.IsNullOrWhiteSpace(name)
            ? "The admin assistant runs on the agent dialogue.admin_agent names, and it names none. "
              + $"Pick one of [{string.Join(", ", config.Agents.Keys)}] in Settings > Dialogue."
            : $"dialogue.admin_agent names the agent '{name}', which is not configured. "
              + $"Known agents: [{string.Join(", ", config.Agents.Keys)}].");
    }
}
