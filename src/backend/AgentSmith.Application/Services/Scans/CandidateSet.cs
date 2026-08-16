using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: the three fates of a finding nobody vouched for, before any model is asked.
/// <para>
/// <see cref="Refutable"/> points at code that can be read, so it can be put to a
/// refuter. <see cref="Unresolvable"/> cites a file the scan cannot read at all — an
/// invented location, and the one case where a finding is dropped outright.
/// <see cref="NotFromSource"/> is a vulnerable package or a secret in git history:
/// reading today's source refutes neither, so it passes through untouched.
/// </para>
/// </summary>
public sealed record CandidateSet(
    IReadOnlyList<CandidateFinding> Refutable,
    IReadOnlyList<SkillObservation> Unresolvable,
    IReadOnlyList<SkillObservation> NotFromSource);
