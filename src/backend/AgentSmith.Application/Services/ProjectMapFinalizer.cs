using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <inheritdoc cref="IProjectMapFinalizer"/>
public sealed class ProjectMapFinalizer(
    IProjectMapJsonReader mapJsonReader,
    IRunContextAccessor runContext,
    ILogger<ProjectMapFinalizer> logger) : IProjectMapFinalizer
{
    private const string FinalizeInstruction =
        "Your exploration budget is exhausted. Reply now with ONLY the JSON object "
        + "describing the repository, based on the evidence you gathered so far. "
        + "Omit fields you found no evidence for. No prose, no tool calls.";

    public async Task<(ProjectMap? Map, string Error)> FinalizeAsync(
        IChatClient chat, ChatOptions options, List<ChatMessage> messages,
        ChatResponse exploration, int attempt, string? repoName,
        CancellationToken cancellationToken)
    {
        messages.AddRange(exploration.Messages);
        messages.Add(new ChatMessage(ChatRole.User, FinalizeInstruction));
        using var _scope = runContext.BeginCallScope(
            "project-analyzer", "BootstrapDiscover", repoName);
        var response = await chat.GetResponseAsync(
            messages, FinalizeOptions(options), cancellationToken);
        if (mapJsonReader.TryRead(response.Text ?? string.Empty, out var map, out var error))
            return (map, string.Empty);
        logger.LogWarning(
            "ProjectAnalyzer attempt {Attempt} finalize turn still unparseable: {Error}. "
            + "Raw response (truncated): {Raw}",
            attempt, error, Truncate(response.Text));
        return (null, error);
    }

    // Tools stay declared (Anthropic rejects a request whose history contains tool
    // blocks without the tools param) but ToolMode=None forbids further calls.
    private static ChatOptions FinalizeOptions(ChatOptions options) => new()
    {
        Tools = options.Tools,
        ToolMode = ChatToolMode.None,
        MaxOutputTokens = options.MaxOutputTokens,
    };

    private static string Truncate(string? text, int max = 2000) =>
        string.IsNullOrEmpty(text) ? "<empty>"
        : text.Length <= max ? text
        : text[..max] + $"… (+{text.Length - max} more chars)";
}
