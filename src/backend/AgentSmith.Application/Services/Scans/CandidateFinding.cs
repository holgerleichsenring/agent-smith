using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: a finding nobody has vouched for, together with the evidence it points at.
/// <para>
/// The scanners raise it; the scan master's SILENCE used to promote it straight to
/// delivery. It is a candidate now — its citation resolved against something the scan
/// really holds, and that thing carried along so a refuter can be shown the evidence
/// rather than the headline.
/// </para>
/// <para>
/// p0429a: the evidence is source for a repo claim and the real request/response for a
/// live-target one. Both are quoted back by the refuter and both are checked against what
/// it was shown, so the surface only changes what the prompt calls it.
/// </para>
/// </summary>
public sealed record CandidateFinding(
    SkillObservation Observation,
    string Location,
    string Evidence,
    EvidenceSurface Surface = EvidenceSurface.Source);
