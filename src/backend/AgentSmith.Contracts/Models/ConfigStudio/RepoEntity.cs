namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// Editable studio view of one repo catalog entry. <see cref="Name"/> is the
/// repo locator (URL or path); <see cref="Branch"/> its default branch.
/// </summary>
public sealed record RepoEntity(
    string Id,
    string Name,
    string? Branch)
{
    public RepoEntity() : this(string.Empty, string.Empty, null) { }
}
