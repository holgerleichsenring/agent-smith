using AgentSmith.Infrastructure.Models;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Runs the agentic tool-calling loop against the Claude API.
/// Sends messages, processes tool calls, feeds results back until the agent is done.
/// </summary>
public sealed class AgenticLoop(
    AnthropicClient client,
    string model,
    ToolExecutor toolExecutor,
    ILogger logger,
    CacheConfig cacheConfig,
    TokenUsageTracker tracker,
    CompactionConfig compactionConfig,
    IContextCompactor? compactor = null,
    IProgressReporter? progressReporter = null,
    int maxIterations = 25)
{
    public async Task<IReadOnlyList<CodeChange>> RunAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<Message>
        {
            new() { Role = RoleType.User, Content = new List<ContentBase> { new TextContent { Text = userMessage } } }
        };

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            logger.LogDebug("Agentic loop iteration {Iteration}", iteration + 1);
            ReportDetail($"\ud83d\udd04 Iteration {iteration + 1}...");

            var response = await SendRequestAsync(systemPrompt, messages, cancellationToken);
            tracker.Track(response);
            LogTokenUsage(response, iteration + 1);
            AppendAssistantResponse(messages, response);

            if (!HasToolUse(response))
            {
                logger.LogInformation(
                    "Agent completed after {Iterations} iterations", iteration + 1);
                break;
            }

            var toolResults = await ProcessToolCalls(response, cancellationToken);
            AppendToolResults(messages, toolResults);

            messages = await TryCompactAsync(messages, iteration + 1, cancellationToken);
        }

        tracker.LogSummary(logger);
        return toolExecutor.GetChanges();
    }

    private async Task<MessageResponse> SendRequestAsync(
        string systemPrompt,
        List<Message> messages,
        CancellationToken cancellationToken)
    {
        var parameters = new MessageParameters
        {
            Model = model,
            MaxTokens = AgentDefaults.DefaultMaxTokens,
            System = new List<SystemMessage> { new(systemPrompt) },
            Messages = messages,
            Tools = ToolDefinitions.All,
            Stream = false,
            PromptCaching = ResolveCacheType()
        };

        return await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
    }

    private static void AppendAssistantResponse(List<Message> messages, MessageResponse response)
    {
        messages.Add(new Message
        {
            Role = RoleType.Assistant,
            Content = response.Content
        });
    }

    private static bool HasToolUse(MessageResponse response)
    {
        return response.Content.OfType<ToolUseContent>().Any();
    }

    private async Task<List<ToolResultContent>> ProcessToolCalls(
        MessageResponse response, CancellationToken cancellationToken)
    {
        var results = new List<ToolResultContent>();

        foreach (var toolUse in response.Content.OfType<ToolUseContent>())
        {
            logger.LogDebug("Executing tool: {Tool}", toolUse.Name);

            var toolResult = await toolExecutor.ExecuteAsync(toolUse.Name, toolUse.Input);
            var isError = toolResult.StartsWith("Error:", StringComparison.Ordinal);

            results.Add(new ToolResultContent
            {
                ToolUseId = toolUse.Id,
                Content = new List<ContentBase>
                {
                    new TextContent { Text = toolResult }
                },
                IsError = isError
            });
        }

        return results;
    }

    private static void AppendToolResults(
        List<Message> messages, List<ToolResultContent> toolResults)
    {
        messages.Add(new Message
        {
            Role = RoleType.User,
            Content = toolResults.Cast<ContentBase>().ToList()
        });
    }

    private async Task<List<Message>> TryCompactAsync(
        List<Message> messages, int iteration, CancellationToken cancellationToken)
    {
        if (!compactionConfig.IsEnabled || compactor is null)
            return messages;

        if (iteration < compactionConfig.ThresholdIterations)
            return messages;

        if (iteration % compactionConfig.ThresholdIterations != 0)
            return messages;

        var keepRecentMessages = compactionConfig.KeepRecentIterations * 2;
        var beforeCount = messages.Count;
        logger.LogInformation(
            "Triggering context compaction at iteration {Iteration}, keeping last {Keep} messages",
            iteration, keepRecentMessages);

        var compacted = await compactor.CompactAsync(messages, keepRecentMessages, cancellationToken);
        ReportDetail($"\u26a1 Context compacted ({beforeCount} \u2192 {compacted.Count} messages)");
        return compacted;
    }

    private PromptCacheType ResolveCacheType()
    {
        if (!cacheConfig.IsEnabled) return PromptCacheType.None;
        return cacheConfig.Strategy.ToLowerInvariant() switch
        {
            "automatic" => PromptCacheType.AutomaticToolsAndSystem,
            "fine-grained" => PromptCacheType.FineGrained,
            _ => PromptCacheType.None
        };
    }

    private void ReportDetail(string text)
    {
        try { progressReporter?.ReportDetailAsync(text).GetAwaiter().GetResult(); }
        catch (Exception ex) { logger.LogDebug(ex, "Detail reporting failed"); }
    }

    private void LogTokenUsage(MessageResponse response, int iteration)
    {
        var usage = response.Usage;
        logger.LogDebug(
            "Iteration {Iteration} tokens: Input={Input}, Output={Output}, " +
            "CacheCreate={CacheCreate}, CacheRead={CacheRead}",
            iteration,
            usage.InputTokens,
            usage.OutputTokens,
            usage.CacheCreationInputTokens,
            usage.CacheReadInputTokens);
    }
}
