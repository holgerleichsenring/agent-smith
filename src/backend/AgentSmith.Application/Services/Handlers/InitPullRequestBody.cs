using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-15-c6e9: the init pull request's body, composed from what the rounds actually did.
/// <para>
/// It was a fixed literal — "Auto-generated project context, code map, and coding principles" —
/// equally true of a run that transferred principles into an empty repository and of one that
/// preserved a ratified file and wrote nothing. The pull request is the one artefact a human
/// opens to ratify what the framework wrote, so it is where the round's facts belong; a step
/// result is a run record nobody opens.
/// </para>
/// <para>
/// Its own type because <see cref="InitCommitHandler"/> sits exactly at its file-length
/// baseline — and because a body composed from run facts is testable on its own, which a
/// string interpolated inside a commit handler is not.
/// </para>
/// </summary>
internal static class InitPullRequestBody
{
    private const string Lead = "Auto-generated project context, code map, and coding principles.";

    /// <summary>
    /// The body for <paramref name="repoName"/>, followed by <paramref name="siblingMarker"/>.
    /// <para>
    /// The marker is appended here rather than by the caller because a later handler replaces it
    /// with the sibling pull-request list and fails hard when it is absent — composing a body
    /// that dropped it would turn a reporting improvement into a broken pull-request chain.
    /// </para>
    /// </summary>
    public static string Compose(PipelineContext pipeline, string repoName, string siblingMarker)
    {
        pipeline.TryGet<List<BootstrapRoundOutcome>>(ContextKeys.BootstrapOutcomes, out var outcomes);
        return Compose(outcomes ?? [], repoName, siblingMarker);
    }

    /// <summary>
    /// The composition itself, over the facts rather than over the run that produced them —
    /// which is what lets it be asserted directly instead of through a pipeline.
    /// </summary>
    public static string Compose(
        IReadOnlyList<BootstrapRoundOutcome> outcomes, string repoName, string siblingMarker)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        var mine = outcomes.Where(o => o.RepoName == repoName).ToList();
        if (mine.Count == 0) return $"{Lead}\n\n{siblingMarker}";

        var body = new StringBuilder(Lead).Append("\n\n");
        foreach (var outcome in mine.OrderBy(o => o.ContextName, StringComparer.Ordinal))
        {
            body.Append("- **").Append(outcome.ContextName).Append("** — ")
                .Append(Principles(outcome.Mode)).Append('\n');
            foreach (var artefact in outcome.Artefacts)
                body.Append("  - `").Append(artefact.Path).Append("` — ")
                    .Append(Artefact(artefact)).Append('\n');
        }
        return body.Append('\n').Append(siblingMarker).ToString();
    }

    private static string Principles(PrinciplesMode mode) => mode switch
    {
        PrinciplesMode.Transferred =>
            "coding principles transferred from the authored core and language delta; "
            + "review and merge to ratify them",
        // Never claim a transfer that did not happen: a repository whose principles were
        // already ratified is told nothing was touched.
        PrinciplesMode.PreservedExisting =>
            "coding principles already present and left untouched",
        _ => "coding principles authored by the skill — the resolved catalog shipped no core",
    };

    private static string Artefact(ArtefactWrite artefact) => artefact.Status switch
    {
        ArtefactStatus.Written => "written from the language delta; it enforces the rules above",
        ArtefactStatus.PreservedExisting => "already present and left untouched",
        // The operator ratified nothing here — this run wrote it, for an earlier component of
        // the same repository. Saying "preserved" would credit them with our own write.
        ArtefactStatus.AlreadyWrittenThisRound => "written for an earlier context of this repository",
        _ => $"NOT written — {artefact.Reason ?? "no reason recorded"}",
    };
}
