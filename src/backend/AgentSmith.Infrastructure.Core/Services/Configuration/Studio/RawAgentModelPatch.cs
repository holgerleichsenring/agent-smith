using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// Applies a studio agent's per-role model routing onto the raw agent config: the
/// reserved role <c>coding</c> is the agent's top-level model/deployment pair, every
/// other role patches an entry of the <c>models:</c> registry. Split out of
/// <see cref="RawConfigPatch"/> (2026-08-27-3eb1) — role routing is its own reason to
/// change, and it changed the moment a role gained a stated input window.
/// <para>
/// A role saved with an empty model is UNSET: it is removed and inherits again, which is
/// how an operator clears a role. Nothing here writes a model the operator did not name.
/// </para>
/// </summary>
internal static class RawAgentModelPatch
{
    public static void Apply(AgentEntity entity, AgentConfig agent)
    {
        if (entity.Models.TryGetValue("coding", out var coding) && !string.IsNullOrWhiteSpace(coding.Model))
        {
            agent.Model = coding.Model;
            agent.Deployment = coding.Deployment;
        }
        var registryRoles = entity.Models.Where(kv => kv.Key != "coding").ToList();
        if (registryRoles.Count == 0) return;

        agent.Models ??= new ModelRegistryConfig();
        foreach (var (role, assignment) in registryRoles)
            PatchAssignment(agent.Models, role, assignment);
    }

    private static void PatchAssignment(ModelRegistryConfig registry, string role, AgentModelAssignment source)
    {
        if (!StudioModelRoles.IsKnown(role))
            throw new ConfigurationException(
                $"Unknown agent model role '{role}' (known: coding, {string.Join(", ", StudioModelRoles.Names)}).");
        if (string.IsNullOrWhiteSpace(source.Model))
        {
            StudioModelRoles.Set(registry, role, null);
            return;
        }
        var target = StudioModelRoles.Get(registry, role) ?? new ModelAssignment();
        target.Model = source.Model;
        target.Deployment = source.Deployment;
        if (source.MaxTokens is { } maxTokens) target.MaxTokens = maxTokens;
        // 2026-08-27-3eb1: same patch semantics as MaxTokens — null keeps what is stored.
        if (source.ContextWindowTokens is { } window) target.ContextWindowTokens = window;
        StudioModelRoles.Set(registry, role, target);
    }
}
