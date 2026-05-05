using AgentSmith.Infrastructure.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Compaction;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Runs the agentic tool-calling loop against the OpenAI Chat Completions API.
/// Mirrors the Claude AgenticLoop but uses OpenAI SDK types. Optional context
/// compaction (p0114) flat-lines token growth on long agentic-execute runs.
/// </summary>
public sealed class OpenAiAgenticLoop(
    ChatClient client,
    ToolExecutor toolExecutor,
    ILogger logger,
    TokenUsageTracker tracker,
    IProgressReporter progressReporter,
    int maxIterations,
    IOpenAiContextCompactor? compactor = null)
{
    public async Task<IReadOnlyList<CodeChange>> RunAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userMessage)
        };

        var options = new ChatCompletionOptions();
        foreach (var tool in OpenAiToolDefinitions.All)
            options.Tools.Add(tool);

        CompactionEvent? pendingCompaction = null;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            logger.LogDebug("OpenAI agentic loop iteration {Iteration}", iteration + 1);
            ReportDetail($"\ud83d\udd04 Iteration {iteration + 1}...", cancellationToken);

            ChatCompletion completion = await client.CompleteChatAsync(
                messages, options, cancellationToken);

            pendingCompaction = FinalizePendingCompaction(pendingCompaction, completion);
            TrackUsage(completion, iteration + 1);
            messages.Add(new AssistantChatMessage(completion));

            if (completion.FinishReason != ChatFinishReason.ToolCalls)
            {
                logger.LogInformation(
                    "OpenAI agent completed after {Iterations} iterations", iteration + 1);
                break;
            }

            var toolResults = await ProcessToolCallsAsync(completion, cancellationToken);
            foreach (var result in toolResults)
                messages.Add(result);

            // COMPACTION POINT \u2014 runs synchronously between rounds, after a complete
            // assistant\u2192tool-results round. Must NEVER fire while a tool_call is
            // in-flight or unanswered. Future parallelization of the agentic loop
            // must move this point or guard it explicitly.
            if (compactor is not null)
            {
                var estimated = OpenAiContextCompactor.EstimateTokens(messages);
                var compactionResult = await compactor.CompactIfNeededAsync(messages, iteration + 1, estimated, cancellationToken);
                if (compactionResult.Event is not null)
                {
                    messages = compactionResult.Messages.ToList();
                    if (compactionResult.Event.Failed)
                        logger.LogWarning("OpenAi loop: compaction failed \u2014 {Reason}", compactionResult.Event.FailureReason);
                    else
                        pendingCompaction = compactionResult.Event;
                }
            }
        }

        tracker.LogSummary(logger);
        return toolExecutor.GetChanges();
    }

    private CompactionEvent? FinalizePendingCompaction(CompactionEvent? pending, ChatCompletion completion)
    {
        if (pending is null || completion.Usage is null) return pending;
        var finalized = pending.WithVerifiedTokens(completion.Usage.InputTokenCount);

        // Attribute the summarizer's own LLM call to the run total under a
        // "compaction" phase so the cost calculation matches what the provider
        // charged. Input and output tokens are tracked separately because their
        // billing rates differ.
        if (finalized.SummarizerInputTokens > 0 || finalized.SummarizerOutputTokens > 0)
        {
            tracker.SetPhase("compaction");
            tracker.Track(finalized.SummarizerInputTokens, finalized.SummarizerOutputTokens);
            tracker.SetPhase("primary");
        }

        logger.LogInformation(
            "Compacted {Old}\u2192{New} messages; verified {Verified} input tokens (saved est. {Saved}; summarizer cost {SumIn} input + {SumOut} output tokens; prompt {Hash})",
            finalized.OldMessageCount, finalized.NewMessageCount,
            finalized.PostCompactionVerifiedTokens, finalized.VerifiedSavedTokens,
            finalized.SummarizerInputTokens, finalized.SummarizerOutputTokens, finalized.PromptHash);
        return null;
    }

    private async Task<List<ToolChatMessage>> ProcessToolCallsAsync(
        ChatCompletion completion, CancellationToken cancellationToken)
    {
        var results = new List<ToolChatMessage>();

        foreach (var toolCall in completion.ToolCalls)
        {
            logger.LogDebug("Executing tool: {Tool}", toolCall.FunctionName);

            JsonNode? input = null;
            if (!string.IsNullOrEmpty(toolCall.FunctionArguments.ToString()))
            {
                input = JsonNode.Parse(toolCall.FunctionArguments.ToString());
            }

            var toolResult = await toolExecutor.ExecuteAsync(toolCall.FunctionName, input);
            results.Add(new ToolChatMessage(toolCall.Id, toolResult));
        }

        return results;
    }

    private void TrackUsage(ChatCompletion completion, int iteration)
    {
        var usage = completion.Usage;
        if (usage is null) return;

        var inputTokens = usage.InputTokenCount;
        var outputTokens = usage.OutputTokenCount;

        tracker.Track(inputTokens, outputTokens);

        logger.LogDebug(
            "OpenAI iteration {Iteration} tokens: Input={Input}, Output={Output}",
            iteration, inputTokens, outputTokens);
    }

    private void ReportDetail(string text, CancellationToken cancellationToken)
    {
        try { progressReporter?.ReportDetailAsync(text, cancellationToken).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }
}
