namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// Editable studio view of one MCP server catalog entry. <see cref="AuthSecret"/>
/// carries the env-NAME of the auth token — never a value.
/// </summary>
public sealed record McpServerEntity(
    string Id,
    string Transport,
    string? Url,
    string? AuthSecret)
{
    public McpServerEntity() : this(string.Empty, string.Empty, null, null) { }
}
