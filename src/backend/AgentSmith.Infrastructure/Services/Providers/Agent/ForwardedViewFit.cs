using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// 2026-10-07-6b9dc: shrinks the tool results of a message list, largest first, until its
/// estimate is under a token target — in a COPY. The caller's list and every message and
/// result in it stay as they were: the stored history is what later iterations, decorators
/// and the trace read, and only this one forwarded request has to fit.
/// <para>
/// Each cut goes through <see cref="ToolResultBound.Recut"/>, so a result keeps the true total
/// its first bound stated and carries one marker, never a second contradictory one. A result
/// is never cut below <see cref="FloorChars"/>: below that the marker alone is most of it.
/// </para>
/// </summary>
internal static class ForwardedViewFit
{
    internal const int FloorChars = 4_000;

    // Mirrors CompactingChatClient's estimate (~4 characters per token).
    private const int CharsPerToken = 4;

    /// <summary>
    /// The list itself when it already estimates under <paramref name="targetTokens"/>;
    /// otherwise a copy whose largest tool results are re-cut until it does or every
    /// result is at the floor.
    /// </summary>
    internal static IList<ChatMessage> Fit(IList<ChatMessage> messages, int targetTokens)
    {
        var estimate = CompactingChatClient.EstimateTokens(messages);
        if (estimate < targetTokens) return messages;
        var copy = new List<ChatMessage>(messages);
        while (estimate >= targetTokens && Largest(copy, FloorChars) is { } slot)
        {
            var excessChars = (estimate - targetTokens + 1) * CharsPerToken;
            Replace(copy, slot, Math.Max(FloorChars, slot.Text.Length - excessChars));
            estimate = CompactingChatClient.EstimateTokens(copy);
        }
        return copy;
    }

    /// <summary>The estimated tokens the tool results of <paramref name="messages"/> account for.</summary>
    internal static int ToolResultTokens(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Sum(r => r.Result?.ToString()?.Length ?? 0) / CharsPerToken;

    /// <summary>The largest text tool result longer than <paramref name="minChars"/>; null when none is.</summary>
    internal static ToolResultSlot? Largest(IList<ChatMessage> messages, int minChars = 0)
    {
        ToolResultSlot? largest = null;
        for (var m = 0; m < messages.Count; m++)
            for (var c = 0; c < messages[m].Contents.Count; c++)
                if (messages[m].Contents[c] is FunctionResultContent result
                    && ToolResultBound.TextOf(result.Result) is { } text
                    && text.Length > minChars && text.Length > (largest?.Text.Length ?? -1))
                    largest = new ToolResultSlot(m, c, result.CallId, text);
        return largest;
    }

    // A new message around a new result; CallId, the other contents and their order are kept.
    // RawRepresentation is dropped on both: a provider that finds its own raw message sends
    // that instead of Contents, which would forward the uncut text.
    private static void Replace(List<ChatMessage> messages, ToolResultSlot slot, int budget)
    {
        var message = messages[slot.Message];
        var original = (FunctionResultContent)message.Contents[slot.Content];
        var contents = new List<AIContent>(message.Contents)
        {
            [slot.Content] = new FunctionResultContent(original.CallId, ToolResultBound.Recut(slot.Text, budget))
            {
                Exception = original.Exception,
                AdditionalProperties = original.AdditionalProperties,
            },
        };
        var clone = message.Clone();
        clone.Contents = contents;
        clone.RawRepresentation = null;
        messages[slot.Message] = clone;
    }
}
