namespace AgentSmith.Server.Models;

/// <summary>
/// The project + repo set a spec-dialog session is grounded on. Chosen at
/// session start from the connection/project catalog; scopes every downstream
/// grounding and the eventual ticket's tracker.
/// </summary>
public sealed record ActiveScope
{
    public required string Project { get; init; }
    public IReadOnlyList<string> Repos { get; init; } = [];
}
