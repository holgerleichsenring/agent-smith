using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-7c9ba: whether a run's next revision would say nothing the branch does not already say.
/// A plain re-trigger that ran no model, pinned no question, had nothing discarded and is not a preview
/// or a hand-back carries no input; when the series files it renders equal the branch set's, a revision
/// would be a commit and a ticket comment about nothing. Resume is left out on purpose: a run that died
/// between its revision and its cut comment needs the resume to post the comment that anchors the thread.
/// </summary>
public sealed class SpecSetUnchanged(SeriesFiles files)
{
    public bool Holds(
        SpecSet finalized, SpecSetReadResult? previous, SpecSourceResolver.Decision decision,
        bool questionPinned, bool preview)
    {
        ArgumentNullException.ThrowIfNull(finalized);
        ArgumentNullException.ThrowIfNull(decision);
        if (previous is null || decision.NeedsModel || questionPinned || preview || finalized.IsHandedBack) return false;
        if (decision.Cause != SpecRevisionCause.Retrigger || decision.Discarded is not null) return false;
        if (string.IsNullOrWhiteSpace(finalized.Series)) return false;
        var branch = previous.Set with { Series = finalized.Series };
        return Same(files.Render(finalized with { Revisions = branch.Revisions }), files.Render(branch));
    }

    private static bool Same(IReadOnlyList<SpecSetFile> next, IReadOnlyList<SpecSetFile> branch) =>
        next.Count == branch.Count
        && next.Zip(branch).All(pair => string.Equals(pair.First.Path, pair.Second.Path, StringComparison.Ordinal)
            && string.Equals(pair.First.Content, pair.Second.Content, StringComparison.Ordinal));
}
