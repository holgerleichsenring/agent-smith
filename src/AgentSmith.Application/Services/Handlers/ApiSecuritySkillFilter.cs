using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Restricts the available skill pool based on ActiveMode and ApiSourceAvailable.
/// Passive + no source collapses the pool to the minimum 4-skill set; missing
/// source removes code-aware skills regardless of mode.
/// </summary>
public sealed class ApiSecuritySkillFilter
{
    private static readonly HashSet<string> PassiveNoSourcePool = new(StringComparer.OrdinalIgnoreCase)
    {
        "recon-analyst", "anonymous-attacker", "false-positive-filter", "chain-analyst"
    };

    private static readonly HashSet<string> CodeAwareSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "auth-config-reviewer", "ownership-checker", "upload-validator-reviewer"
    };

    public IReadOnlyList<RoleSkillDefinition> Filter(
        IReadOnlyList<RoleSkillDefinition> roles, bool activeMode, bool sourceAvailable)
    {
        if (!activeMode && !sourceAvailable)
            return roles.Where(r => PassiveNoSourcePool.Contains(r.Name)).ToList();

        if (!sourceAvailable)
            return roles.Where(r => !CodeAwareSkills.Contains(r.Name)).ToList();

        return roles;
    }
}
