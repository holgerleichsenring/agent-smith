namespace AgentSmith.Server.Models;

/// <summary>
/// Configuration for TeamsAdapter, bound from environment variables.
/// </summary>
public sealed class TeamsAdapterOptions
{
    public string AppId { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}
