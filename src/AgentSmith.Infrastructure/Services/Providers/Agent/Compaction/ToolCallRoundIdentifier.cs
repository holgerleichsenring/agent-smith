using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;

/// <summary>
/// Finds the boundary that separates the "tail" (last N complete tool-call rounds, kept verbatim)
/// from the "prefix" (older messages, eligible for summarization).
///
/// A round is one of:
///  • an <see cref="AssistantChatMessage"/> with tool_calls + all <see cref="ToolChatMessage"/>
///    responses to those tool_call_ids (one logical exchange).
///  • a bare <see cref="AssistantChatMessage"/> with no tool_calls (terminal reply).
///
/// Walking backward from the end of the message list, we never split a round mid-pair —
/// the OpenAI API rejects payloads where a tool message references a tool_call_id without
/// the matching assistant message in the same array.
/// </summary>
internal static class ToolCallRoundIdentifier
{
    /// <summary>
    /// Returns the index at which the tail begins — everything from index 0 up to (but not
    /// including) the returned index is "prefix" eligible for summarization.
    /// Returns <paramref name="messages"/>.Count when N=0 or no rounds can be formed.
    /// Returns 0 when all messages already fit in N rounds (no compaction needed).
    /// </summary>
    public static int FindTailStartIndex(IReadOnlyList<ChatMessage> messages, int completeRounds)
    {
        if (completeRounds <= 0 || messages.Count == 0) return messages.Count;

        var roundsFound = 0;
        var i = messages.Count - 1;

        while (i >= 0 && roundsFound < completeRounds)
        {
            while (i >= 0 && messages[i] is ToolChatMessage) i--;

            if (i < 0 || messages[i] is not AssistantChatMessage) break;

            roundsFound++;
            i--;
        }

        return i + 1;
    }
}
