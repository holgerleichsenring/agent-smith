using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: the three fates of a delivered finding, before any model is asked.
/// <para>
/// <see cref="Refutable"/> resolved against evidence the scan really holds — a file it can
/// read, or an endpoint the loaded specification declares — so it can be put to a refuter.
/// <see cref="Unresolvable"/> cites a file the scan cannot read or an endpoint the
/// specification does not contain: an invented location. <see cref="Unanswerable"/> is a
/// vulnerable package, a secret in git history, a live claim made with no specification
/// loaded, or a line beyond the end of a file that really exists — nothing the scan holds
/// can speak to it, so it passes through untouched.
/// </para>
/// <para>
/// 2026-09-01-85b2: <see cref="Unresolvable"/> is a fate, not a sentence. Only a finding
/// NOBODY authored is dropped for one; a master's own finding whose cited path the reader
/// could not open is delivered exactly as it was written.
/// </para>
/// </summary>
public sealed record CandidateSet(
    IReadOnlyList<CandidateFinding> Refutable,
    IReadOnlyList<SkillObservation> Unresolvable,
    IReadOnlyList<SkillObservation> Unanswerable);
