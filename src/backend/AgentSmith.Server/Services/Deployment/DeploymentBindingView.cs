namespace AgentSmith.Server.Services.Deployment;

/// <summary>2026-11-18-3863c: one deployment binding as the dashboard offers it — the context, its repository when named,
/// the template it is delivered after, and its environments.</summary>
public sealed record DeploymentBindingView(
    string Context, string? ContextRepo, string Template, IReadOnlyList<DeploymentEnvironmentView> Environments);
