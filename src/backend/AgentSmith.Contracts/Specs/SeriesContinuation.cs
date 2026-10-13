namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-9a59l: what an approval given in a ticket's own conversation does to that ticket's
/// series, recorded beside the approved set and applied by the ticket's next run — by spec id, never
/// by position.
/// </summary>
/// <param name="Dispositions">Every spec the approval named or the basis held, in proposal order and
/// then in basis order: what each one becomes.</param>
/// <param name="BasisRunId">The attempt whose series the approval continues; null when no code
/// attempt worked the ticket, which is a never-worked hand-off the run never merges.</param>
public sealed record SeriesContinuation(IReadOnlyList<SpecDisposition> Dispositions, string? BasisRunId);

/// <summary>One spec's fate under an approval — <see cref="SpecDispositionKinds"/>.</summary>
public sealed record SpecDisposition(string SpecId, string Kind);
