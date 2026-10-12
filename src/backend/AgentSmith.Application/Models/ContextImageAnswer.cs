namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-12-50bdb: what write_context_yaml says after a successful write, and the
/// trust-boundary refusal (2026-10-12-6bfac) the round's outcome carries on.
/// </summary>
public sealed record ContextImageAnswer(string Text, ContextImageRefusal? Refusal);
