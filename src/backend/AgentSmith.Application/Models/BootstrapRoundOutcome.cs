using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-15-c6e9: what one bootstrap round did to one context of one repository, kept so the
/// init pull request can say it.
/// <para>
/// The round's own sentence goes to a step result, which is a run record an operator does not
/// open. The pull request is the artefact a human opens to RATIFY what the framework wrote, and
/// it carried a fixed literal that was equally true of every run.
/// </para>
/// <para>
/// 2026-10-03-cf20c: <c>Overlays</c> names each framework overlay composed into the file, so
/// the operator ratifies it in the open — never in an HTML comment the rendered PR hides.
/// </para>
/// </summary>
public sealed record BootstrapRoundOutcome(
    string RepoName,
    string ContextName,
    PrinciplesMode Mode,
    IReadOnlyList<ArtefactWrite> Artefacts,
    IReadOnlyList<string>? Overlays = null)
{
    /// <summary>2026-10-03-cf20c: the framework overlays the written principles.md carries.</summary>
    public IReadOnlyList<string> Overlays { get; init; } = Overlays ?? [];
}
