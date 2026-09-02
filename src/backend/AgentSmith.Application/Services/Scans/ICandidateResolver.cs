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

    /// <summary>The candidate with its evidence attached, or the reason there is none:
    /// an invented citation, which may cost an unauthored finding its place, or a real
    /// citation with no showable evidence, which costs nothing.</summary>
    Task<CandidateResolution> ResolveAsync(
        SkillObservation finding, ScanEvidence evidence, CancellationToken cancellationToken);
}
