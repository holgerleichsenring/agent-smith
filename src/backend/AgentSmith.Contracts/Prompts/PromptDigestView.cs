namespace AgentSmith.Contracts.Prompts;

/// <summary>
/// 2026-11-18-e06bd: one model call in two lines — what it was fed (the last message it was sent) and what it decided
/// (the answer its successor in the tool loop carries). Either is null when the store does not hold it.
/// </summary>
public sealed record PromptDigestView(string PromptId, string? Fed, string? Decided);
