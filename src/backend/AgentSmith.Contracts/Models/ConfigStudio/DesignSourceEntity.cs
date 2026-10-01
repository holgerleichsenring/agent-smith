namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// 2026-10-01-7f7aa: editable studio view of one design source. <see cref="AuthSecret"/> is
/// the NAME of a catalog secret holding the access token — never a value; the store refuses a
/// name the secrets catalog does not hold, and refuses deleting a secret a source names.
/// </summary>
public sealed record DesignSourceEntity(
    string Id,
    string Vendor,
    string AuthSecret,
    string? DisplayName = null)
{
    public DesignSourceEntity() : this(string.Empty, string.Empty, string.Empty) { }
}
