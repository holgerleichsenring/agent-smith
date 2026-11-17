using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-17-ec48a: what one spec-scope reviewer of a run is given for a text — extracted from
/// <see cref="SpecReviewLoop"/> so the master's one revision review (<see cref="SpecRevisionReview"/>) builds the
/// same input the pre-build rounds do.
/// </summary>
public static class SpecReviewInput
{
    public static SpecAttackInput Of(
        SpecReviewRun run, SpecDraft draft, int round, SpecRoundEvidenceResult evidence,
        IReadOnlyList<SpecReviewResidue> residues)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(evidence);
        return new(draft, SpecReviewScopes.Spec, round, 0, run.WiderGoal,
            SpecToday.Describes(draft, run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet)),
            evidence.Problems, evidence.Unchecked, evidence.Total, residues, run.QuestionPin)
        {
            RepositoryNames = SpecReviewRepositories.Names(run.Pipeline), OwnerWords = run.OwnerWords, // 2026-11-15-53d0
        };
    }
}
