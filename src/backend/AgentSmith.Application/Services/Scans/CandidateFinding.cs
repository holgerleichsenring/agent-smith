using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: a finding nobody has vouched for, together with the code it points at.
/// <para>
/// The scanners raise it; the scan master's SILENCE used to promote it straight to
/// delivery. It is a candidate now — its citation resolved against files the scan really
/// read, and the code at that line carried along so a refuter can be shown the thing
/// rather than the headline.
/// </para>
/// </summary>
public sealed record CandidateFinding(SkillObservation Observation, string Location, string Code);
