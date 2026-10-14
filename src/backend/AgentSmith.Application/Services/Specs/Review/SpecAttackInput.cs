using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: everything one fresh attack reviewer is given — it has no conversation behind it.
/// </summary>
/// <param name="Draft">The spec under review, its yaml shown whole.</param>
/// <param name="Scope">One of <see cref="SpecReviewScopes"/>.</param>
/// <param name="Round">The round, 1-based.</param>
/// <param name="Reviewer">The reviewer within the round, 0-based.</param>
/// <param name="WiderGoal">What the person asked for and why, when known.</param>
/// <param name="DescribesToday">False when the spec's requires name an unbuilt member of its set — its
/// facts then describe the tree those requires leave.</param>
/// <param name="EvidenceProblems">The round's evidence problems, found before any reviewer was asked.</param>
/// <param name="NotChecked">Citations the round could not check, minted look lines included.</param>
/// <param name="CitationsTotal">Every citation the round counted.</param>
/// <param name="PriorResidues">Wording the previous round's cuts left standing in another register.</param>
public sealed record SpecAttackInput(
    SpecDraft Draft,
    string Scope,
    int Round,
    int Reviewer,
    string? WiderGoal,
    bool DescribesToday,
    IReadOnlyList<SpecEvidenceProblem> EvidenceProblems,
    int NotChecked,
    int CitationsTotal,
    IReadOnlyList<SpecReviewResidue> PriorResidues)
{
    /// <summary>Sandbox key to repository name (<see cref="SpecReviewRepositories.Names"/>): a look names
    /// a sandbox key, stored evidence names the repository. A key missing here is stored as itself.</summary>
    public IReadOnlyDictionary<string, string> RepositoryNames { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
