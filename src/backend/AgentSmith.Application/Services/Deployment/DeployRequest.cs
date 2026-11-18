namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: what a deploy run delivers — a context of the project, to an environment, at a version
/// (null asks the run to take it from the environment the template says this one promotes from). 2026-11-18-3863h:
/// <paramref name="PromotedVersion"/> is the version the server's promotion carries from a settled source deploy.</summary>
public sealed record DeployRequest(
    string Context, string? ContextRepo, string Environment, string? Version, string? PromotedVersion = null);
