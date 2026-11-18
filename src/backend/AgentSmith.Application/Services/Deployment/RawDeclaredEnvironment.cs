namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c: one environment of deployment.yaml, as read. 2026-11-18-3863e: pipeline, ref and inputs.</summary>
internal sealed class RawDeclaredEnvironment
{
    public string? Name { get; set; }
    public string? Manifest { get; set; }
    public string? VersionKey { get; set; }
    public string? PromotesFrom { get; set; }
    public string? Pipeline { get; set; }
    public string? Ref { get; set; }
    public Dictionary<string, string>? Inputs { get; set; }
}
