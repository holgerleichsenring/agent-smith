namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// Editable studio view of one project. <see cref="Agent"/>, <see cref="Tracker"/>
/// and <see cref="Repos"/> are catalog references (names) — the referential
/// validator rejects any that is not present in the catalog, so a broken wiring
/// can never be persisted.
/// </summary>
public sealed record ProjectEntity(
    string Id,
    string Agent,
    string Tracker,
    IReadOnlyList<string> Repos,
    string? Trigger,
    IReadOnlyList<string> Pipelines)
{
    public ProjectEntity() : this(string.Empty, string.Empty, string.Empty, [], null, []) { }
}
