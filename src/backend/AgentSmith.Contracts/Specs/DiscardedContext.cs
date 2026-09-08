namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-09-08-1830: a context the scope call named that the derivation deliberately
/// left out, with the reason a reviewer reads — the context-level twin of
/// <see cref="DiscardedSegment"/>. A named context is carried by a phase or listed
/// here; a cut that silently covers fewer contexts than the ticket names is the
/// failure this record exists for.
/// </summary>
public sealed record DiscardedContext(string Context, string Reason);
