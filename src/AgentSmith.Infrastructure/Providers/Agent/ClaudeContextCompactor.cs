using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// LLM-based context compactor that uses a fast/cheap model (e.g. Haiku)
/// to summarize old conversation history, preserving key decisions and file paths.
/// </summary>
public sealed class ClaudeContextCompactor(
    AnthropicClient client,
    string summaryModel,
    ILogger logger,
    TokenUsageTracker? usageTracker = null) : IContextCompactor
{
    private const string SummarySystemPrompt = """
        You are a context compactor. Summarize the following conversation history between
        an AI assistant and tool calls. Preserve:
        - File paths that were read or modified
        - Key decisions and reasoning
        - Error messages and how they were resolved
        - The current state of the implementation

        Omit:
        - Raw file contents (just note which files were read)
        - Redundant tool call/result pairs
        - Verbose command output (just note the outcome)

        Be concise but complete. The summary will be used as context for continuing the work.
        """;

    public async Task<List<Message>> CompactAsync(
        List<Message> messages,
        int keepRecentMessages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count <= keepRecentMessages)
            return messages;

        var splitIndex = messages.Count - keepRecentMessages;
        var oldMessages = messages.Take(splitIndex).ToList();
        var recentMessages = messages.Skip(splitIndex).ToList();

        var oldContent = ExtractTextContent(oldMessages);

        if (string.IsNullOrWhiteSpace(oldContent))
        {
            logger.LogDebug("No text content to compact, returning messages as-is");
            return messages;
        }

        logger.LogInformation(
            "Compacting context: {OldCount} old messages → summary, keeping {RecentCount} recent",
            oldMessages.Count, recentMessages.Count);

        var summary = await GenerateSummaryAsync(oldContent, cancellationToken);

        var compacted = new List<Message>
        {
            new()
            {
                Role = RoleType.User,
                Content = new List<ContentBase>
                {
                    new TextContent
                    {
                        Text = $"[Context Summary from previous iterations]\n{summary}"
                    }
                }
            }
        };

        // Ensure valid message alternation: if first recent message is also User role,
        // we need an assistant acknowledgment in between
        if (recentMessages.Count > 0 && recentMessages[0].Role == RoleType.User)
        {
            compacted.Add(new Message
            {
                Role = RoleType.Assistant,
                Content = new List<ContentBase>
                {
                    new TextContent { Text = "Understood. Continuing with the implementation." }
                }
            });
        }

        compacted.AddRange(recentMessages);

        logger.LogInformation(
            "Context compacted: {OldCount} → {NewCount} messages",
            messages.Count, compacted.Count);

        return compacted;
    }

    private async Task<string> GenerateSummaryAsync(
        string content, CancellationToken cancellationToken)
    {
        var response = await client.Messages.GetClaudeMessageAsync(
            new MessageParameters
            {
                Model = summaryModel,
                MaxTokens = 2048,
                System = new List<SystemMessage> { new(SummarySystemPrompt) },
                Messages = new List<Message>
                {
                    new()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase>
                        {
                            new TextContent { Text = content }
                        }
                    }
                },
                Stream = false
            },
            cancellationToken);

        var previousPhase = TrackCompactionUsage(response);

        var summary = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? "";

        logger.LogDebug(
            "Summary generated: {InputTokens} input → {OutputTokens} output tokens",
            response.Usage.InputTokens, response.Usage.OutputTokens);

        RestorePhase(previousPhase);

        return summary;
    }

    private static string ExtractTextContent(List<Message> messages)
    {
        var parts = new List<string>();

        foreach (var message in messages)
        {
            var role = message.Role == RoleType.Assistant ? "Assistant" : "User/Tool";

            foreach (var content in message.Content)
            {
                switch (content)
                {
                    case TextContent text:
                        parts.Add($"[{role}] {text.Text}");
                        break;

                    case ToolUseContent toolUse:
                        parts.Add($"[Assistant] Called tool: {toolUse.Name}");
                        break;

                    case ToolResultContent toolResult:
                        {
                            var resultText = toolResult.Content?
                                .OfType<TextContent>()
                                .Select(t => t.Text)
                                .FirstOrDefault() ?? "(no output)";
                            // Truncate long tool results to avoid sending too much to the summary model
                            if (resultText.Length > 2000)
                                resultText = resultText[..2000] + "\n... [truncated for summary]";
                            parts.Add($"[Tool Result] {resultText}");
                            break;
                        }
                }
            }
        }

        return string.Join("\n\n", parts);
    }

    private string? TrackCompactionUsage(MessageResponse response)
    {
        if (usageTracker is null)
            return null;

        var previousPhase = "primary";
        usageTracker.SetPhase("compaction");
        usageTracker.Track(response);
        return previousPhase;
    }

    private void RestorePhase(string? previousPhase)
    {
        if (previousPhase is not null)
            usageTracker?.SetPhase(previousPhase);
    }
}
