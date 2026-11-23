namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-0219: queues one policy evaluation again by project and evaluation id. A delegate rather than the SDK's
/// PolicyHttpClient, as <see cref="AzureReposPolicyEvaluations"/> is, so a test can answer it.
/// </summary>
public delegate Task AzureReposPolicyRequeue(Guid projectId, Guid evaluationId, CancellationToken cancellationToken);
