using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Applies the review's findings to a derived phase, within a CLOSED set of shapes: a
/// criterion is replaced by the observation that decides it, and nothing else happens.
/// <para>
/// The closure is the whole safety argument. A critic free to reshape what it is judged by
/// will make the contract satisfiable, which is how a loop ends up carrying its own engine.
/// So a correction may only swap one criterion for a stated replacement; it may not drop a
/// criterion, add one, weaken the goal, or touch a phase that already ran. Anything the
/// review wants that does not fit belongs to the author, and <see cref="Apply"/> reports it
/// as unapplied rather than approximating it.
/// </para>
/// <para>
/// The swap is TEXTUAL and refuses to guess: the criterion must appear verbatim in the
/// phase's yaml, inside its done-list. A replacement the text cannot carry exactly is not
/// applied — an edit nobody can predict is worse than a hand-back.
/// </para>
/// </summary>
public static class SpecReviewCorrection
{
    public static (SpecPhase Phase, IReadOnlyList<CriterionReview> Unapplied) Apply(
        SpecPhase phase, IReadOnlyList<CriterionReview> findings)
    {
        ArgumentNullException.ThrowIfNull(phase);
        ArgumentNullException.ThrowIfNull(findings);
        var yaml = phase.Draft.Yaml;
        var done = phase.Draft.Done.ToList();
        var unapplied = new List<CriterionReview>();
        foreach (var finding in findings)
        {
            var applied = TryApply(finding, ref yaml, done);
            if (!applied) unapplied.Add(finding);
        }
        return (phase with { Draft = phase.Draft with { Yaml = yaml, Done = done } }, unapplied);
    }

    private static bool TryApply(CriterionReview finding, ref string yaml, List<string> done)
    {
        var replacement = finding.Replacement?.Trim();
        if (!finding.IsCorrectable || string.IsNullOrWhiteSpace(replacement)) return false;
        var index = done.FindIndex(c => string.Equals(c, finding.Criterion, StringComparison.Ordinal));
        if (index < 0) return false;
        var swapped = SwapInDoneList(yaml, finding.Criterion, replacement);
        if (swapped is null) return false;
        yaml = swapped;
        done[index] = replacement;
        return true;
    }

    /// <summary>The swap happens after the done: marker, so a criterion whose words also
    /// appear in the goal or a step cannot be edited by accident.</summary>
    private static string? SwapInDoneList(string yaml, string criterion, string replacement)
    {
        var marker = yaml.IndexOf("\ndone:", StringComparison.Ordinal);
        if (marker < 0) return null;
        var at = yaml.IndexOf(criterion, marker, StringComparison.Ordinal);
        return at < 0 ? null : string.Concat(yaml.AsSpan(0, at), replacement, yaml.AsSpan(at + criterion.Length));
    }
}
