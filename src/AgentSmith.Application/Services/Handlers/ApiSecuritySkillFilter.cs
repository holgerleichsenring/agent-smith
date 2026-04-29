using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Restricts the available skill pool based on ActiveMode, ApiSourceAvailable
/// and signal-driven gates (correlated findings, header findings). Passive +
/// no source collapses the pool to the minimum 4-skill set; missing source
/// removes code-aware skills regardless of mode. security-headers-auditor
/// also stays in passive-no-source mode when header findings exist.
/// </summary>
public sealed class ApiSecuritySkillFilter
{
    private static readonly HashSet<string> PassiveNoSourcePool = new(StringComparer.OrdinalIgnoreCase)
    {
        "recon-analyst", "anonymous-attacker", "false-positive-filter", "chain-analyst"
    };

    private static readonly HashSet<string> CodeAwareSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "auth-config-reviewer", "ownership-checker", "upload-validator-reviewer",
        "controller-implementation-reviewer"
    };

    public IReadOnlyList<RoleSkillDefinition> Filter(
        IReadOnlyList<RoleSkillDefinition> roles, bool activeMode, bool sourceAvailable,
        bool hasHeaderFindings = false)
    {
        if (!activeMode && !sourceAvailable)
        {
            return roles
                .Where(r => PassiveNoSourcePool.Contains(r.Name)
                    || (hasHeaderFindings && r.Name.Equals("security-headers-auditor", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (!sourceAvailable)
            return roles.Where(r => !CodeAwareSkills.Contains(r.Name)).ToList();

        return roles;
    }
}
