namespace AgentSmith.Contracts.Events;

/// <summary>
/// Per-LLM-call attribution ambient pushed by handlers around their
/// <c>.GetResponseAsync</c> invocation. Read by <c>EventPublishingChatClient</c>
/// + <c>EventPublishingAIFunction</c> at event-emission time so
/// <c>LlmCallStarted/Finished</c> and <c>ToolCall/Result</c> events carry
/// role + phase + repo without threading the values through every
/// decorator constructor. <see cref="Phase"/> is a free-text string so
/// Contracts stays Application-enum-free; producers conventionally pass
/// <c>SkillExecutionPhase.ToString()</c>.
/// </summary>
public sealed record CallScope(string Role, string Phase, string? RepoName);
