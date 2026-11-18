namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: one environment as the template declares it.</summary>
/// <param name="Manifest">The manifest's path in the environment's repository.</param>
/// <param name="VersionKey">The dotted path (mapping keys, sequence indexes) of the scalar carrying the version.</param>
/// <param name="PromotesFrom">The environment whose version this one takes; null for an entry environment.</param>
public sealed record DeclaredEnvironment(string Name, string Manifest, string VersionKey, string? PromotesFrom);
