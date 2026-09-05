using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// What the ticket author reads when the review cannot correct a finding.
/// <para>
/// "The ticket is ambiguous" is read once and never again. What survives contact with a
/// person who is not in the room is the criterion, the fact that contradicts it, and
/// something concrete to accept instead — so the reply is a decision, not homework.
/// </para>
/// </summary>
public static class SpecReviewHandbackReason
{
    public static string For(string phaseId, IReadOnlyList<CriterionReview> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        var body = string.Join("\n\n", findings.Select(Describe));
        return $"Phase {phaseId} carries {findings.Count} completion criterion(s) that no work "
            + $"can satisfy in this repository:\n\n{body}";
    }

    private static string Describe(CriterionReview finding) =>
        $"- \"{finding.Criterion}\"\n"
        + $"  {Why(finding.Disposition)}\n"
        + $"  Looked: {finding.Observation}\n"
        + $"  Found: {finding.Output}"
        + (string.IsNullOrWhiteSpace(finding.Replacement)
            ? string.Empty
            : $"\n  Suggested instead: \"{finding.Replacement}\"");

    private static string Why(SpecReviewDisposition disposition) => disposition switch
    {
        SpecReviewDisposition.PrescribesShape =>
            "states what the solution must look like, not what must be true afterwards — "
            + "the repository decides whether that shape is reachable, and here it is not.",
        SpecReviewDisposition.NoObservationSettles =>
            "rests on a judgement, so nothing observable can ever mark it met.",
        SpecReviewDisposition.AlreadyTrue =>
            "already holds before any work starts.",
        _ => "cannot be met as stated.",
    };
}
