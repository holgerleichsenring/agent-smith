using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429a: one kind of citation, resolved against the one kind of evidence that can answer
/// it. A resolver that cannot answer a finding says so, and the finding passes through
/// untouched instead of being dropped by a check that was never about it.
/// </summary>
public interface ICandidateResolver
{
    /// <summary>Is this finding's citation the kind this resolver checks, and is the
    /// evidence it needs actually present?</summary>
    bool CanAnswer(SkillObservation finding, ScanEvidence evidence);

    /// <summary>The candidate with its evidence attached, or null when the citation
    /// resolves against nothing — which is invention, and the finding is dropped.</summary>
    Task<CandidateFinding?> ResolveAsync(
        SkillObservation finding, ScanEvidence evidence, CancellationToken cancellationToken);
}
