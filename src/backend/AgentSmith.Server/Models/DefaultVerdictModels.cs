namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-8762p: one item of a verdict batch — a listed default, <c>ratified</c> or <c>overruled</c>, an optional note.</summary>
public sealed record DefaultVerdictRequest(long Id, string Verdict, string? Note = null);

/// <summary>2026-10-13-8762p: what a verdict batch did — the ids it decided, and the ones already decided with their standing verdict.</summary>
public sealed record DefaultVerdictResult(IReadOnlyList<long> Applied, IReadOnlyList<DefaultVerdictStanding> AlreadyDecided);

/// <summary>2026-10-13-8762p: a default somebody decided first, and how.</summary>
public sealed record DefaultVerdictStanding(long Id, string Verdict);
