using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// What the studio refuses to store about a role's <c>use:</c> (2026-09-30-62bab): a name the
/// agent's catalog does not declare, and a role carrying both <c>use:</c> and an inline model —
/// the chain would ignore the model, so storing it would keep a setting nothing reads. A file
/// loaded at boot is only advised about the first (ModelRoleFindings) and may carry the second.
/// </summary>
internal static class CatalogUseRule
{
    public static void Validate(IReadOnlyDictionary<string, AgentConfig> agents)
    {
        foreach (var (name, agent) in agents)
        {
            foreach (var (role, use) in new ModelRoleChain(agent).UnknownUses())
                throw new ConfigurationException(
                    $"Agent '{name}': role '{role}' uses catalog entry '{use}', which the catalog does not declare.");
            if (agent.Models is not { } models) continue;
            foreach (var slot in ModelRoleSlots.All.Where(s => BothSet(s.Get(models))))
                throw new ConfigurationException(
                    $"Agent '{name}': role '{slot.Key}' carries both use: and an inline model; keep one.");
        }
    }

    private static bool BothSet(ModelAssignment? role) =>
        !string.IsNullOrWhiteSpace(role?.Use) && !string.IsNullOrWhiteSpace(role.Model);
}
