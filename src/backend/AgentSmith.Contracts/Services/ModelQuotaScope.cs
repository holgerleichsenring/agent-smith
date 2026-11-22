namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-66e4a: the deployment a quota is counted on — its <paramref name="Key"/> (one key, one budget), and
/// the provider and deployment name a person reads.
/// </summary>
public sealed record ModelQuotaScope(string Key, string Provider, string Deployment);
