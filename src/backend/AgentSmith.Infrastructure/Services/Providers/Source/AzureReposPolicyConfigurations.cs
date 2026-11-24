using Microsoft.TeamFoundation.Policy.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>2026-11-24-f9c5d: reads a project's build-validation policy configurations. A delegate rather than the SDK's
/// PolicyHttpClient, as <see cref="AzureReposPolicyEvaluations"/> is, so a test can answer it.</summary>
public delegate Task<IReadOnlyList<PolicyConfiguration>> AzureReposPolicyConfigurations(Guid projectId, CancellationToken cancellationToken);
