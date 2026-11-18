namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: deployment.yaml as YamlDotNet reads it, before the parser checks it. 2026-11-18-3863e: the
/// parameters and each environment's pipeline keys.</summary>
internal sealed class RawDeploymentDeclaration
{
    public string? Delivery { get; set; }
    public List<RawDeploymentParameter>? Parameters { get; set; }
    public List<RawDeclaredEnvironment>? Environments { get; set; }
}
