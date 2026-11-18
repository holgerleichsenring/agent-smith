namespace AgentSmith.Server.Services.Deployment;

/// <summary>2026-11-18-3863c: one bound environment — whether a person approves its deliveries, and the repository
/// holding its manifest (null: the context's own).</summary>
public sealed record DeploymentEnvironmentView(string Name, bool Approval, string? Repo);
