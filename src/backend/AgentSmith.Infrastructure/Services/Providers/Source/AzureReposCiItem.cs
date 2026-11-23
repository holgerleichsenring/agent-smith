using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: one Azure DevOps CI signal on a pull request — a build-validation policy's evaluation (with the
/// build it ran) or a pull-request status — as a check, with what a failure links to and says.
/// </summary>
public sealed record AzureReposCiItem(CiCheck Check, int? BuildId, string? Url, string? Description)
{
    /// <summary>2026-11-23-0219: the policy evaluation behind a build-policy check — what a re-queue names; null for a status.</summary>
    public Guid? EvaluationId { get; init; }
}
