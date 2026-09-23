using System.Text;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: turns the messages a call adds into the one prompt a session accepts.
///
/// A Copilot session takes a prompt string, not a role-tagged message list: SendMessageItem
/// carries Prompt, DisplayPrompt, Attachments, RequiredTool, Billable and Source, and Source is a
/// provenance tag (user / system / command-id / schedule-id / agent-id), not a message role. A
/// single user message is therefore sent as itself; anything else is labelled, so the model can
/// still tell an assistant turn from a tool result.
/// </summary>
internal static class CopilotPromptRenderer
{
    /// <summary>Sent when a call adds no new message — the caller wants the turn continued.</summary>
    internal const string ContinuationPrompt = "Continue.";

    internal static string Render(IReadOnlyList<ChatMessage> messages)
    {
        if (messages.Count == 0) return string.Empty;
        if (messages.Count == 1 && messages[0].Role == ChatRole.User)
            return messages[0].Text ?? string.Empty;

        var builder = new StringBuilder();
        foreach (var message in messages)
        {
            var text = message.Text;
            if (string.IsNullOrEmpty(text)) continue;
            if (builder.Length > 0) builder.Append("\n\n");
            if (message.Role != ChatRole.User)
                builder.Append('[').Append(Label(message.Role)).Append("]\n");
            builder.Append(text);
        }
        return builder.ToString();
    }

    /// <summary>
    /// The system prompt a session is CREATED with. It replaces Copilot's own rather than
    /// appending to it, so the run's instructions are the only ones in force.
    /// </summary>
    internal static string? SystemMessageOf(IReadOnlyList<ChatMessage> messages)
    {
        var system = messages.Where(m => m.Role == ChatRole.System).Select(m => m.Text).Where(t => !string.IsNullOrEmpty(t)).ToList();
        return system.Count == 0 ? null : string.Join("\n\n", system);
    }

    private static string Label(ChatRole role) =>
        role == ChatRole.Assistant ? "assistant"
        : role == ChatRole.Tool ? "tool result"
        : role == ChatRole.System ? "system"
        : role.Value;
}
