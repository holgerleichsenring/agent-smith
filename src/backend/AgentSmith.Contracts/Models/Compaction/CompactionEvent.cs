namespace AgentSmith.Contracts.Models.Compaction;

/// <summary>
/// Telemetry record emitted by a context compactor on each fire (success or failure).
/// Pre-compaction estimate drives the trigger decision; post-compaction value is the
/// authoritative <c>usage.prompt_tokens</c> from the next API response, populated when
/// the agentic loop makes the next chat-completions call.
/// </summary>
public sealed record CompactionEvent(
    int OldMessageCount,
    int NewMessageCount,
    int PreCompactionEstimatedTokens,
    int? PostCompactionVerifiedTokens,
    int SummarizerInputTokens,
    int SummarizerOutputTokens,
    string PromptHash,
    bool Failed,
    string? FailureReason)
{
    /// <summary>Total billed tokens for the summarizer's own LLM call (input + output combined).
    /// Use the split fields when computing cost — input and output rates differ.</summary>
    public int SummarizerTotalTokens => SummarizerInputTokens + SummarizerOutputTokens;

    public int? VerifiedSavedTokens =>
        PostCompactionVerifiedTokens is null ? null
        : PreCompactionEstimatedTokens - PostCompactionVerifiedTokens.Value;

    public CompactionEvent WithVerifiedTokens(int verifiedTokens) =>
        this with { PostCompactionVerifiedTokens = verifiedTokens };

    public static CompactionEvent ForFailure(int oldCount, int estimatedTokens, string promptHash, string reason) =>
        new(
            OldMessageCount: oldCount,
            NewMessageCount: oldCount, // unchanged on failure
            PreCompactionEstimatedTokens: estimatedTokens,
            PostCompactionVerifiedTokens: null,
            SummarizerInputTokens: 0,
            SummarizerOutputTokens: 0,
            PromptHash: promptHash,
            Failed: true,
            FailureReason: reason);
}
