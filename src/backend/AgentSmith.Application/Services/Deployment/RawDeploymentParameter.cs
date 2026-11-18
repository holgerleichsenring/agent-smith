namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863e: one declared parameter of deployment.yaml, as read.</summary>
internal sealed class RawDeploymentParameter
{
    public string? Name { get; set; }
    public string? Default { get; set; }
}
