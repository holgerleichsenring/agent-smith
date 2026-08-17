using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: the three fates of a finding nobody vouched for, before any model is asked.
/// <para>
/// <see cref="Refutable"/> resolved against evidence the scan really holds — a file it can
/// read, or an endpoint the loaded specification declares — so it can be put to a refuter.
/// <see cref="Unresolvable"/> cites a file the scan cannot read or an endpoint the
/// specification does not contain: an invented location, and the one case where a finding
/// is dropped outright. <see cref="Unanswerable"/> is a vulnerable package, a secret in git
/// history, or a live claim made with no specification loaded — nothing the scan holds can
/// speak to it, so it passes through untouched.
/// </para>
/// </summary>
public sealed record CandidateSet(
    IReadOnlyList<CandidateFinding> Refutable,
    IReadOnlyList<SkillObservation> Unresolvable,
    IReadOnlyList<SkillObservation> Unanswerable);
