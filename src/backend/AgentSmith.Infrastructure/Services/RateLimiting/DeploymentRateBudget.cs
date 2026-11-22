using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4b: the budget a deployment runs on. One deployment can be reached through several agents (the
/// catalog's, a project's, a pipeline override), so each number is the lowest any configured agent on that
/// deployment sets in <c>agent.rate_limit</c> — the rule max_concurrent_runs follows — and the building agent's
/// provider default where none sets one. An agent without an override therefore never lowers another's budget to
/// its provider default, and two agents never take turns with their numbers. The configuration is read as it stands
/// now, so a studio edit reaches the deployment when its next client is built.
/// </summary>
public sealed class DeploymentRateBudget(IServiceProvider services)
{
    public LlmRateLimitOptions For(AgentConfig agent, ModelQuotaScope scope, string providerType, Func<string?> anthropicKey)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(scope);
        var own = LlmRateBudget.For(agent, providerType, anthropicKey);
        // The configuration as it stands now; the building agent's own only when no configured agent is on the
        // deployment, so a run started before an edit cannot keep pulling the budget back to its old numbers.
        var onDeployment = OnDeployment(scope.Key).ToList();
        var stated = (onDeployment.Count > 0 ? onDeployment : [agent]).Select(a => a.RateLimit).OfType<RateLimitConfig>().ToList();
        return own with
        {
            RequestsPerMinute = stated.Select(r => r.RequestsPerMinute).Min() ?? own.RequestsPerMinute,
            InputTokensPerMinute = stated.Select(r => r.InputTokensPerMinute).Min() ?? own.InputTokensPerMinute,
        };
    }

    private IEnumerable<AgentConfig> OnDeployment(string key) =>
        services.GetService<ConfiguredAgents>()?.Agents.Where(a => Keys(a).Contains(key, StringComparer.Ordinal)) ?? [];

    // Every deployment an agent's roles reach.
    private static IEnumerable<string> Keys(AgentConfig agent)
    {
        var roles = new ModelRoleChain(agent);
        return Enum.GetValues<TaskType>()
            .Select(roles.TryFor)
            .OfType<ModelAssignment>()
            .Select(assignment => ModelQuotaKey.For(agent, assignment, assignment.ProviderType ?? agent.Type).Key)
            .Distinct(StringComparer.Ordinal);
    }
}
