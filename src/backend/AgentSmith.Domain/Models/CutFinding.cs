namespace AgentSmith.Domain.Models;

/// <summary>
/// p0422: one reason a phase cannot be delivered as written.
/// <para>
/// <see cref="Criterion"/> is quoted verbatim from the phase, and the framework checks
/// that the phase really states it — a reviewer that invents its objection is worse than
/// none, because it blocks a cut nobody can find the fault in.
/// </para>
/// <para>
/// 2026-09-15-ffa7: <see cref="Cites"/> is the evidence id of a look the reviewer took. A
/// false premise is admitted by it — the framework checks that it minted that id — and a
/// quote alone never admits one.
/// </para>
/// </summary>
public sealed record CutFinding(
    string PhaseId,
    string Criterion,
    string Problem,
    string Why,
    string? ConflictsWith = null,
    string? Cites = null);
