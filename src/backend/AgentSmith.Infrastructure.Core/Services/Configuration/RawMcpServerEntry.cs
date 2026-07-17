namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Raw YAML shape for one entry inside the top-level <c>mcp_servers:</c> catalog.
/// The config studio owns this catalog; the loader binds it so it survives an
/// export round-trip. <c>Auth</c> holds a secret NAME, never a value.
/// </summary>
public sealed class RawMcpServerEntry
{
    public string Transport { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Auth { get; set; }
}
