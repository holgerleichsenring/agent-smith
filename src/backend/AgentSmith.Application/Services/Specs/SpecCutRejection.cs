using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: turns a cut review into the correction the deriver is handed back.
/// <para>
/// It names the phase, quotes the criterion and says what is wrong with it — the same
/// rejection a parser error gets, because the deriver already knows how to answer one.
/// </para>
/// <para>
/// 2026-09-15-ffa7: a finding that cites a look carries the LINE that id names, resolved
/// from the reviewer's evidence. The id alone means nothing to the deriver: its own looks
/// are minted under another letter, and the reviewer's never entered its conversation.
/// </para>
/// </summary>
public static class SpecCutRejection
{
    public static string For(SpecCutReview review, IReadOnlyList<string>? evidence)
    {
        ArgumentNullException.ThrowIfNull(review);
        var byId = DerivationEvidence.IndexById(evidence);
        return string.Join("\n", review.Findings.Select(finding => Line(finding, byId)));
    }

    private static string Line(CutFinding finding, IReadOnlyDictionary<string, string> byId) =>
        $"- {finding.PhaseId}: \"{finding.Criterion}\" — {finding.Problem}: {finding.Why}"
        + (finding.ConflictsWith is null ? string.Empty : $" (conflicts with \"{finding.ConflictsWith}\")")
        + (DerivationEvidence.CitationsIn(finding.Cites).FirstOrDefault(byId.ContainsKey) is { } id
            ? $" (evidence: {byId[id]})"
            : string.Empty);
}
