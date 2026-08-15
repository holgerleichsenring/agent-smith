using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: turns a cut review into the correction the deriver is handed back.
/// <para>
/// It names the phase, quotes the criterion and says what is wrong with it — the same
/// rejection a parser error gets, because the deriver already knows how to answer one.
/// </para>
/// </summary>
public static class SpecCutRejection
{
    public static string For(SpecCutReview review)
    {
        ArgumentNullException.ThrowIfNull(review);
        return string.Join("\n", review.Findings.Select(Line));
    }

    private static string Line(CutFinding finding) =>
        $"- {finding.PhaseId}: \"{finding.Criterion}\" — {finding.Problem}: {finding.Why}"
        + (finding.ConflictsWith is null ? string.Empty : $" (conflicts with \"{finding.ConflictsWith}\")");
}
