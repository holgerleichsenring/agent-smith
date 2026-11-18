namespace AgentSmith.Server.Services.Deployment;

/// <summary>2026-11-18-3863c: a person's deploy — the context, its repository when two declare the name, the environment,
/// the version (empty to promote) and the ticket whose page lists the run, if any.</summary>
public sealed record DeployLaunchRequest(
    string Context, string Environment, string? Version = null, string? ContextRepo = null, string? TicketId = null);
