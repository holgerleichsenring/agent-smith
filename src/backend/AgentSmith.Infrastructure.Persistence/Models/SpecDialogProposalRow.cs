namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>2026-11-17-a0b6d: one turn's stored proposal — its run, JSON, when it was last written, and its decision
/// id and state (both null while it was never put to a decision).</summary>
public sealed record SpecDialogProposalRow(string RunId, string Json, DateTimeOffset At, string? ProposalId, string? State);
