namespace AgentSmith.Domain.Models;

/// <summary>
/// p0422: one reason a phase cannot be delivered as written.
/// <para>
/// <see cref="Criterion"/> is quoted verbatim from the phase, and the framework checks
/// that the phase really states it — a reviewer that invents its objection is worse than
/// none, because it blocks a cut nobody can find the fault in.
/// </para>
/// </summary>
public sealed record CutFinding(
    string PhaseId,
    string Criterion,
    string Problem,
    string Why,
    string? ConflictsWith = null);
