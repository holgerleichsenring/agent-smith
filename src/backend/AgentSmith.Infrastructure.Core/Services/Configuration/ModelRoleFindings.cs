using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// What the model role chain says about this configuration, said once at load. Everything
/// here is ADVISORY — nothing is refused, so one odd row never fails the whole configuration:
/// <list type="bullet">
/// <item>a role whose <c>use:</c> names a catalog entry the agent does not declare — the run
/// that reaches that role fails, and this says so before it does;</item>
/// <item>a <c>models:</c> block that leaves primary, scout, planning or summarization unset
/// now inherits where it used to get a built-in Claude model;</item>
/// <item>a non-Claude agent whose roles resolve to one of those former built-in ids;</item>
/// <item>a role that decides structure resolving to a catalog entry the operator marked
/// <c>fast</c>. An untiered entry is never reported — the product does not rate models.</item>
/// </list>
/// </summary>
public sealed class ModelRoleFindings
{
    private static readonly HashSet<string> FormerBuiltInModels =
        new(StringComparer.OrdinalIgnoreCase) { "claude-haiku-4-5-20251001", "claude-sonnet-4-20250514" };

    private static readonly HashSet<string> ClaudeTypes = new(StringComparer.OrdinalIgnoreCase) { "claude", "anthropic" };

    private static readonly TaskType[] FormerlyDefaulted =
        [TaskType.Primary, TaskType.Scout, TaskType.Planning, TaskType.Summarization];

    public IReadOnlyList<StartupFinding> For(IReadOnlyDictionary<string, AgentConfig> agents) =>
        [.. agents.SelectMany(pair => Findings(pair.Key, pair.Value))];

    private static IEnumerable<StartupFinding> Findings(string name, AgentConfig agent)
    {
        var chain = new ModelRoleChain(agent);
        return chain.UnknownUses().Select(u => UnknownUse(name, u.Role, u.Use))
            .Concat(Inheriting(name, chain))
            .Concat(ClaudeDefaults(name, agent, chain))
            .Concat(FastOnStrong(name, agent, chain));
    }

    private static StartupFinding UnknownUse(string name, string role, string use) =>
        Advisory($"agents.{name}.models.{role}.use",
            $"Agent '{name}' role '{role}' uses catalog entry '{use}', which the agent's catalog does not "
            + "declare. A run that reaches this role fails; declare the entry under catalog: or name one that is.");

    private static IEnumerable<StartupFinding> Inheriting(string name, ModelRoleChain chain)
    {
        var inheriting = chain.InheritingFormerDefaults();
        if (inheriting.Count > 0)
            yield return Advisory($"agents.{name}.model",
                $"Agent '{name}' declares a models: block that leaves {string.Join(", ", inheriting)} unset; "
                + $"those roles answer on '{chain.TryFor(TaskType.Primary)?.Model}' "
                + "(primary from the agent's model, the others from primary), with primary's max_tokens. "
                + "An unset role used to fall back to a built-in Claude model; name the role under "
                + "models: to choose another.");
    }

    private static IEnumerable<StartupFinding> ClaudeDefaults(string name, AgentConfig agent, ModelRoleChain chain)
    {
        var claudeRoles = ClaudeDefaultRoles(agent, chain);
        if (claudeRoles.Count > 0)
            yield return Advisory($"agents.{name}.models",
                $"Agent '{name}' is type '{agent.Type}' but its roles {string.Join(", ", claudeRoles)} name a "
                + "Claude model its provider does not serve — the built-in default a configuration saved "
                + "before 2026-09-28 carries. Clear those roles (an empty model in the configuration studio, "
                + "or remove them from the file) so they inherit the agent's own model.");
    }

    private static IEnumerable<StartupFinding> FastOnStrong(string name, AgentConfig agent, ModelRoleChain chain)
    {
        var fastRoles = FastOnStrongRoles(agent, chain);
        if (fastRoles.Count > 0)
            yield return Advisory($"agents.{name}.models",
                $"Agent '{name}': {string.Join(", ", fastRoles)} — these roles decide structure and need a "
                + "strong model, but the catalog entry they resolve to is marked fast. Assign an entry marked "
                + "strong, or change the entry's tier if it is one.");
    }

    private static IReadOnlyList<string> ClaudeDefaultRoles(AgentConfig agent, ModelRoleChain chain)
    {
        if (ClaudeTypes.Contains(agent.Type) || agent.Models is not { } models) return [];
        return [.. FormerlyDefaulted.Select(ModelRoleSlots.For)
            .Where(slot => slot.Get(models) is not null)
            .Select(slot => (slot.Key, Assignment: Own(slot, models, chain)))
            .Where(r => r.Assignment is { } a && FormerBuiltInModels.Contains(a.Model)
                        && !ClaudeTypes.Contains(a.ProviderType ?? string.Empty))
            .Select(r => r.Key)];
    }

    /// <summary>The assignment a declared role resolves to itself — its entry or its inline
    /// fields — or null when it is unset (it inherits) or names an undeclared entry.</summary>
    private static ModelAssignment? Own(ModelRoleSlot slot, ModelRegistryConfig models, ModelRoleChain chain)
    {
        var declared = slot.Get(models)!;
        return string.IsNullOrWhiteSpace(declared.Use) && string.IsNullOrWhiteSpace(declared.Model)
            ? null
            : chain.TryFor(slot.Task);
    }

    private static IReadOnlyList<string> FastOnStrongRoles(AgentConfig agent, ModelRoleChain chain) =>
        [.. ModelRoleSlots.All
            .Where(slot => slot.NeedsStrong)
            .Select(slot => (slot.Key, Entry: chain.EntryFor(slot.Task)))
            .Where(r => r.Entry is not null && agent.Catalog.TryGetValue(r.Entry, out var entry)
                        && entry.Tier == ModelTier.Fast)
            .Select(r => $"{r.Key} on '{r.Entry}'")];

    private static StartupFinding Advisory(string field, string reason) =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory, reason, Field: field);
}
