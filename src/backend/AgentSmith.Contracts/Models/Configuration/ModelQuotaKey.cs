using AgentSmith.Contracts.Services;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-22-66e4a: the deployment a call's quota is counted on — provider type, effective endpoint and the name the
/// builder sends: the deployment on Azure (<see cref="ModelAssignment.EffectiveDeployment"/>), the model elsewhere.
/// Two roles naming different models on one Azure deployment share its one quota; one model on two endpoints does
/// not. Built here and nowhere else, so the limiter and its read-out cannot count one deployment under two keys.
/// </summary>
public static class ModelQuotaKey
{
    private const string Azure = "azure_openai";

    /// <summary>The scope <paramref name="assignment"/>'s calls through <paramref name="providerType"/> draw on.</summary>
    public static ModelQuotaScope For(AgentConfig agent, ModelAssignment assignment, string providerType)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(assignment);
        var type = providerType.Trim().ToLowerInvariant();
        var endpoint = (assignment.EffectiveEndpoint(agent) ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
        var sent = type == Azure ? assignment.EffectiveDeployment(agent) : Sent(agent, assignment);
        return new ModelQuotaScope($"{type}|{endpoint}|{sent.Trim().ToLowerInvariant()}", type, sent);
    }

    // The model the builder sends; an assignment that names none answers with the agent's primary model.
    private static string Sent(AgentConfig agent, ModelAssignment assignment)
    {
        if (!string.IsNullOrEmpty(assignment.Model)) return assignment.Model;
        var primary = new ModelRoleChain(agent).PrimaryModel;
        return string.IsNullOrEmpty(primary) ? "default" : primary;
    }
}
