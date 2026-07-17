namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0345b: editable studio view of one git-host connection (the p0281a
/// <c>connections:</c> catalog — host/org/auth held ONCE, repos discovered from
/// the provider and selected per project as <c>connection/RepoName</c> refs).
/// <see cref="Type"/> is the host kind (github | gitlab | azure_devops);
/// <see cref="Organization"/> the org/owner/group segment for that kind;
/// <see cref="Project"/> the Azure DevOps team project (null elsewhere).
/// <see cref="AuthSecret"/> carries the env-NAME of the token — never a value.
/// </summary>
public sealed record ConnectionEntity(
    string Id,
    string Type,
    string? Organization,
    string? Project,
    string? AuthSecret,
    string? DefaultBranch)
{
    public ConnectionEntity() : this(string.Empty, string.Empty, null, null, null, null) { }
}
