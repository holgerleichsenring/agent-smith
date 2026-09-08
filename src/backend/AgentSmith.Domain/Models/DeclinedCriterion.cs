namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-06-3d81: one ratified criterion the master DECLINED — disposed of as
/// not applicable because no work inside the repository could make it true — with the
/// evaluated meaning of not doing it.
/// <para>
/// It is a sentence of the ticket, not of the change: the delivery account still judges the
/// branch on its own. What this record exists for is the ticket author, who otherwise never
/// learns which of their sentences no work could satisfy.
/// </para>
/// </summary>
/// <param name="PhaseId">The phase whose master declined it; null on a run that has no
/// phase spec and is judged by a negotiated expectation.</param>
public sealed record DeclinedCriterion(string Criterion, string Reason, string? PhaseId = null);
