using AgentSmith.Contracts.Models.Compaction;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;

/// <summary>
/// Returns the input unchanged. Used when CompactionConfig.IsEnabled is false,
/// when no ChatClient is available for the summarizer, and as a default for
/// non-OpenAI agentic loops that are wired through DI but don't actually compact.
/// </summary>
public sealed class NoOpOpenAiContextCompactor : IOpenAiContextCompactor
{
    public Task<OpenAiCompactionResult> CompactIfNeededAsync(
        IReadOnlyList<ChatMessage> messages,
        int currentIterations,
        int estimatedAccumulatedTokens,
        CancellationToken cancellationToken) =>
        Task.FromResult(new OpenAiCompactionResult(messages, Event: null));
}
