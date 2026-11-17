using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// 2026-11-17-cb32a: where a fold's verbatim tail starts. The tail is sized by the fold TARGET, not only by a count
/// of rounds: a round holding a large tool result kept whole left the view over the trigger, so the next call folded
/// again. The newest assistant message and everything after it (its tool results, a governor reminder) always stay;
/// older messages join while they fit the budget, up to the round count the config keeps.
/// </summary>
internal static class CompactionTail
{
    /// <summary>
    /// The index the tail starts at, never before <paramref name="headCount"/>. <paramref name="skip"/> is a message
    /// the view places elsewhere (the pinned current prompt), so it costs the tail nothing.
    /// </summary>
    internal static int Start(IList<ChatMessage> messages, int headCount, int maxKeep, int budgetTokens, int skip)
    {
        var start = NewestAssistant(messages, headCount);
        var used = 0;
        for (var i = start; i < messages.Count; i++)
            if (i != skip) used += CompactingChatClient.EstimateTokens([messages[i]]);

        var lowest = Math.Max(headCount, messages.Count - maxKeep);
        while (start > lowest)
        {
            var cost = start - 1 == skip ? 0 : CompactingChatClient.EstimateTokens([messages[start - 1]]);
            if (used + cost > budgetTokens) break;
            used += cost;
            start--;
        }

        // Never START on a tool result: its call would be folded away. Advancing drops it into the summary with its
        // call, and stops at the newest assistant message at the latest.
        while (start < messages.Count && IsToolResult(messages[start])) start++;
        return start;
    }

    // The leading system message(s) plus the initial user message — the ticket, kept verbatim through every fold.
    internal static int PinnedHeadCount(IList<ChatMessage> messages)
    {
        var n = 0;
        while (n < messages.Count && messages[n].Role == ChatRole.System) n++;
        if (n < messages.Count && messages[n].Role == ChatRole.User) n++;
        return n;
    }

    private static int NewestAssistant(IList<ChatMessage> messages, int headCount)
    {
        for (var i = messages.Count - 1; i >= headCount; i--)
            if (messages[i].Role == ChatRole.Assistant) return i;
        return Math.Max(headCount, messages.Count - 1);
    }

    private static bool IsToolResult(ChatMessage m) =>
        m.Role == ChatRole.Tool || m.Contents.OfType<FunctionResultContent>().Any();
}
