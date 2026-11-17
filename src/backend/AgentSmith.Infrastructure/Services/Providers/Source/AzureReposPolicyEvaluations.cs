using Microsoft.TeamFoundation.Policy.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: reads a pull request's policy evaluations by project and artifact id. A delegate rather than the
/// SDK's PolicyHttpClient, whose read is not overridable, so a test can answer it.
/// </summary>
public delegate Task<IReadOnlyList<PolicyEvaluationRecord>> AzureReposPolicyEvaluations(
    Guid projectId, string artifactId, CancellationToken cancellationToken);
