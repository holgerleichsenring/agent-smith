namespace AgentSmith.Server.Services.Init;

/// <summary>
/// 2026-10-13-0b82c: the wire shape of one repository in GET /api/projects/{name}/bootstrap — the
/// branch read (its configured name, or <c>default</c> for the host's default), the state
/// (<c>complete</c>, <c>incomplete</c> or <c>unknown</c>), each missing file in words, and why the
/// host could not say when unknown.
/// </summary>
public sealed record ProjectBootstrapEntry(
    string Repo, string Branch, string State, IReadOnlyList<string> Missing, string? Reason)
{
    public const string DefaultBranch = "default";
}
