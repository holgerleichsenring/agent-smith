using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// What the model role chain changed for this configuration, said once at load. Two things,
/// both ADVISORY — the chain already does the right thing and nothing is refused:
/// <list type="bullet">
/// <item>a <c>models:</c> block that leaves primary, scout, planning or summarization unset
/// now inherits where it used to get a built-in Claude model, so a Claude installation that
/// relied on the implicit cheaper scout pays its primary price for it;</item>
/// <item>a non-Claude agent whose roles hold one of those former built-in ids. A configuration
/// imported or saved before the change carries them materialised, and a stored default cannot
/// be told from a choice, so it is reported and never rewritten.</item>
/// </list>
/// </summary>
public sealed class ModelRoleFindings
{
    private static readonly HashSet<string> FormerBuiltInModels =
        new(StringComparer.OrdinalIgnoreCase) { "claude-haiku-4-5-20251001", "claude-sonnet-4-20250514" };

    private static readonly HashSet<string> ClaudeTypes = new(StringComparer.OrdinalIgnoreCase) { "claude", "anthropic" };

    public IReadOnlyList<StartupFinding> For(IReadOnlyDictionary<string, AgentConfig> agents) =>
        [.. agents.SelectMany(pair => Findings(pair.Key, pair.Value))];

    private static IEnumerable<StartupFinding> Findings(string name, AgentConfig agent)
    {
        var inheriting = new ModelRoleChain(agent).InheritingFormerDefaults();
        if (inheriting.Count > 0)
            yield return Advisory($"agents.{name}.model",
                $"Agent '{name}' declares a models: block that leaves {string.Join(", ", inheriting)} unset; "
                + $"those roles answer on '{new ModelRoleChain(agent).For(TaskType.Primary).Model}' "
                + "(primary from the agent's model, the others from primary), with primary's max_tokens. "
                + "An unset role used to fall back to a built-in Claude model; name the role under "
                + "models: to choose another.");
        var claudeRoles = ClaudeDefaultRoles(agent);
        if (claudeRoles.Count > 0)
            yield return Advisory($"agents.{name}.models",
                $"Agent '{name}' is type '{agent.Type}' but its roles {string.Join(", ", claudeRoles)} name a "
                + "Claude model its provider does not serve — the built-in default a configuration saved "
                + "before 2026-09-28 carries. Clear those roles (an empty model in the configuration studio, "
                + "or remove them from the file) so they inherit the agent's own model.");
    }

    private static IReadOnlyList<string> ClaudeDefaultRoles(AgentConfig agent)
    {
        if (ClaudeTypes.Contains(agent.Type) || agent.Models is not { } models) return [];
        (string Role, ModelAssignment? Assignment)[] roles =
        [
            ("primary", models.Primary), ("scout", models.Scout), ("planning", models.Planning),
            ("summarization", models.Summarization),
        ];
        return [.. roles
            .Where(r => r.Assignment is { } a && FormerBuiltInModels.Contains(a.Model)
                        && !ClaudeTypes.Contains(a.ProviderType ?? string.Empty))
            .Select(r => r.Role)];
    }

    private static StartupFinding Advisory(string field, string reason) =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory, reason, Field: field);
}
