using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ed: turns a review of a proposal into the findings that ride on it — each cited
/// id resolved, here and nowhere later, to the framework-minted line it names.
/// <para>
/// The look that minted the id lives only as long as the turn, and the id itself means nothing to
/// an operator or to a later edit turn: the deriver's rejection resolves the same way
/// (<see cref="SpecCutRejection"/>) for the same reason. A finding whose citation resolves to
/// nothing keeps its quote and carries no evidence.
/// </para>
/// <para>
/// WHICH citation is shown follows what ADMITTED the finding: <see cref="SpecCutAdmission"/>
/// keeps a false premise when ANY id it cites names a look that RAN, so showing the first
/// merely resolvable id could put a "proves nothing" line under a finding the framework kept
/// on a different one. The line that carried it is the line the operator is shown.
/// </para>
/// </summary>
public static class SpecDialogProposalFindings
{
    public static IReadOnlyList<ProposalFinding> Of(
        SpecCutReview review, IReadOnlyList<EvidenceLook>? looks)
    {
        ArgumentNullException.ThrowIfNull(review);
        var byId = new Dictionary<string, EvidenceLook>(StringComparer.Ordinal);
        foreach (var look in looks ?? []) byId.TryAdd(look.Id, look);
        return [.. review.Findings.Select(finding => new ProposalFinding(
            finding.PhaseId, finding.Problem, finding.Why,
            string.IsNullOrWhiteSpace(finding.Criterion) ? null : finding.Criterion,
            Cited(finding.Cites, byId)?.Line))];
    }

    /// <summary>The look the finding rests on: the one that RAN where it cites several, and
    /// otherwise the first it cites that the framework minted at all.</summary>
    private static EvidenceLook? Cited(string? cites, IReadOnlyDictionary<string, EvidenceLook> byId)
    {
        var looks = DerivationEvidence.CitationsIn(cites)
            .Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        return looks.Find(look => look.Ran) ?? looks.FirstOrDefault();
    }
}
