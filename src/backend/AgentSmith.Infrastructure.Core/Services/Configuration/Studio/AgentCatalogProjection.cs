using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// Reads a stored agent in catalog form, whatever form it was stored in (2026-09-30-62bab).
/// Declared catalog entries keep their names; a role with <c>use:</c> keeps it. Every other
/// set role — and the agent's own <c>model</c>/<c>deployment</c> primary inherits — becomes an
/// entry: one entry per distinct (model, deployment, max_tokens, window, provider, endpoint),
/// named by its model id, suffixed -2, -3 in role order on a collision. A role whose
/// deployment is unset takes the agent's, because that is the deployment it was called with.
/// Unset roles stay unset and keep inheriting, so no role's call changes.
/// </summary>
internal static class AgentCatalogProjection
{
    public static (Dictionary<string, AgentCatalogModel> Catalog, Dictionary<string, string> Models) Of(
        AgentConfig agent)
    {
        var catalog = agent.Catalog.ToDictionary(kv => kv.Key, kv => ToStudio(kv.Value), StringComparer.Ordinal);
        var models = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in ModelRoleSlots.All)
            if (RoleEntry(agent, slot) is { } role)
                models[slot.Key] = role.Use ?? NameFor(role.Entry!, catalog);
        return (catalog, models);
    }

    private static (string? Use, AgentCatalogModel? Entry)? RoleEntry(AgentConfig agent, ModelRoleSlot slot)
    {
        var declared = agent.Models is { } registry ? slot.Get(registry) : null;
        if (!string.IsNullOrWhiteSpace(declared?.Use)) return (declared.Use, null);
        if (!string.IsNullOrWhiteSpace(declared?.Model)) return (null, Inline(declared, agent.Deployment));
        if (slot.Task != TaskType.Primary || string.IsNullOrWhiteSpace(agent.Model)) return null;
        return (null, Inline(new ModelRoleChain(agent).For(TaskType.Primary), agent.Deployment));
    }

    private static string NameFor(AgentCatalogModel entry, Dictionary<string, AgentCatalogModel> catalog)
    {
        var same = catalog.FirstOrDefault(kv => SameCall(kv.Value, entry)).Key;
        if (same is not null) return same;
        var name = entry.Model;
        for (var n = 2; catalog.ContainsKey(name); n++) name = $"{entry.Model}-{n}";
        catalog[name] = entry;
        return name;
    }

    private static bool SameCall(AgentCatalogModel a, AgentCatalogModel b) =>
        a.Model == b.Model && Blank(a.Deployment) == Blank(b.Deployment)
        && MaxTokens(a) == MaxTokens(b) && a.ContextWindowTokens == b.ContextWindowTokens
        && Blank(a.ProviderType) == Blank(b.ProviderType) && Blank(a.Endpoint) == Blank(b.Endpoint);

    private static AgentCatalogModel Inline(ModelAssignment role, string? agentDeployment) =>
        new(role.Model, Blank(role.Deployment) ?? Blank(agentDeployment), role.MaxTokens, role.ContextWindowTokens,
            Blank(role.ProviderType), Blank(role.Endpoint));

    private static AgentCatalogModel ToStudio(CatalogModel entry) =>
        new(entry.Model, entry.Deployment, entry.MaxTokens, entry.ContextWindowTokens,
            entry.ProviderType, entry.Endpoint, entry.Tier);

    private static int MaxTokens(AgentCatalogModel entry) => entry.MaxTokens ?? new CatalogModel().MaxTokens;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
