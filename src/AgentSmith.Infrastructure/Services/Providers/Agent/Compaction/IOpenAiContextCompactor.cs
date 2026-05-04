using AgentSmith.Contracts.Models.Compaction;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;

/// <summary>
/// Compacts the conversation history of an OpenAI / Azure-OpenAI agentic loop when the
/// trigger threshold is crossed. Provider-specific because <see cref="ChatMessage"/>
/// is OpenAI-SDK-bound — Claude has its own SDK-specific implementation.
/// </summary>
public interface IOpenAiContextCompactor
{
    /// <summary>
    /// Evaluates the trigger and either summarizes the prefix or returns the input unchanged.
    /// Trigger is boolean OR: <c>currentIterations &gt;= ThresholdIterations || estimatedTokens &gt;= MaxContextTokens</c>.
    /// On summarizer failure, returns input messages unchanged with a <see cref="CompactionEvent"/>
    /// where Failed=true — never throws into the agentic loop.
    /// </summary>
    Task<OpenAiCompactionResult> CompactIfNeededAsync(
        IReadOnlyList<ChatMessage> messages,
        int currentIterations,
        int estimatedAccumulatedTokens,
        CancellationToken cancellationToken);
}

public sealed record OpenAiCompactionResult(
    IReadOnlyList<ChatMessage> Messages,
    CompactionEvent? Event);
