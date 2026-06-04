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
public sealed record CallScope(string Role, string Phase, string? RepoName)
{
    /// <summary>
    /// p0222: the agent's one-sentence intent narration for the current turn,
    /// captured by <c>EventPublishingChatClient</c> from the assistant response
    /// text and read by <c>EventPublishingAIFunction</c> when it emits the
    /// turn's ToolCall events. Mutable because the same scope instance spans a
    /// turn's chat call and its subsequent tool invocations (one AsyncLocal
    /// frame); each new turn overwrites it before its tools fire.
    /// </summary>
    public string? Intent { get; set; }
}
