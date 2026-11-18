using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-18-c8e2e: a review cut removes or narrows; it never adds. A cut adds when its replacement holds the whole
/// quote and more — the anchor-and-addition form reviewers used to be taught. Such a cut is not applied: it reaches the
/// author as a finding, the reviewer's words kept, to weigh or to put to the owner.
/// </summary>
public static class SpecCutAddition
{
    public static bool Adds(SpecReviewCut cut)
    {
        ArgumentNullException.ThrowIfNull(cut);
        return cut.Replacement is { } replacement
            && SpecQuotableText.IsRunIn(replacement, cut.Quote)
            && Words(replacement) > Words(cut.Quote);
    }

    /// <summary>The finding an adding cut becomes.</summary>
    public static SpecReviewFinding Finding(SpecReviewCut cut, string? member = null) => new(
        $"A reviewer would add to {member ?? "the spec"} ({cut.Register}): \"{cut.Replacement}\" in place of \"{cut.Quote}\". "
        + "A cut only removes or narrows, so this is not applied: weigh it, or ask the owner when only they can decide.",
        cut.Evidence);

    private static int Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
