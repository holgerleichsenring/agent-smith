using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: what one round's evidence check found — the facts whose cited file or line does not
/// exist, and how many of the <paramref name="Total"/> citations could not be checked at all (a minted look
/// line, a path no sandbox could route, a qualified path). "Clean" means no problem among the citations
/// that WERE checked; the count is what asks the reviewer to open the rest.
/// </summary>
public sealed record SpecRoundEvidenceResult(
    IReadOnlyList<SpecEvidenceProblem> Problems, int Unchecked, int Total)
{
    /// <summary>A spec that was not checked: it does not describe today, or cites nothing.</summary>
    public static SpecRoundEvidenceResult None { get; } = new([], 0, 0);
}
