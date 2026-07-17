namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// Editable studio view of one tracker catalog entry. <see cref="AuthSecret"/>
/// carries the env-NAME of the auth token — never a value.
/// </summary>
public sealed record TrackerEntity(
    string Id,
    string Type,
    string? Org,
    string? Project,
    string? AuthSecret)
{
    public TrackerEntity() : this(string.Empty, string.Empty, null, null, null) { }
}
