using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// Writes a studio agent's models onto the raw agent config in catalog form (2026-09-30-62bab):
/// the catalog replaces the stored one, every role the entity names becomes <c>{use: entry}</c>
/// and nothing else, and the agent's own <c>model</c>/<c>deployment</c> are cleared — primary is
/// required and the chain supplies it to every fallback that read them. A role absent or
/// empty is unset and inherits, which is how an operator clears a role.
/// </summary>
internal static class RawAgentModelPatch
{
    public static void Apply(AgentEntity entity, AgentConfig agent)
    {
        agent.Catalog = entity.Catalog.ToDictionary(kv => kv.Key, kv => ToCatalogModel(kv.Value));
        agent.Model = string.Empty;
        agent.Deployment = null;
        var registry = new ModelRegistryConfig();
        foreach (var (role, use) in entity.Models.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)))
            (ModelRoleSlots.Find(role) ?? throw UnknownRole(role)).Set(registry, new ModelAssignment { Use = use });
        agent.Models = registry;
    }

    private static CatalogModel ToCatalogModel(AgentCatalogModel source) => new()
    {
        Model = source.Model,
        Deployment = Blank(source.Deployment),
        MaxTokens = source.MaxTokens ?? new CatalogModel().MaxTokens,
        ContextWindowTokens = source.ContextWindowTokens,
        ProviderType = Blank(source.ProviderType),
        Endpoint = Blank(source.Endpoint),
        Tier = source.Tier,
    };

    private static ConfigurationException UnknownRole(string role) =>
        new($"Unknown agent model role '{role}' (known: {string.Join(", ", ModelRoleSlots.Keys)}).");

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
