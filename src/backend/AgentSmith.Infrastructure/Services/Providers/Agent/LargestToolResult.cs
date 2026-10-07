using System.Globalization;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// 2026-10-07-6b9dc: the largest tool result in a request a provider refused — which tool
/// produced it, how many characters were forwarded, and the total its bound marker states
/// when it had been cut. A refusal that names this tells the operator which call to narrow.
/// </summary>
internal sealed record LargestToolResult(string ToolName, int ForwardedChars, long? OriginalTotal)
{
    // Above half of the estimate one result IS the overrun: lowering the compaction trigger
    // cannot help, because compaction keeps the recent iterations — where it sits — verbatim.
    private const double DominantShare = 0.5;

    private const int CharsPerToken = 4;

    /// <summary>The largest text tool result in <paramref name="messages"/>; null when there is none.</summary>
    internal static LargestToolResult? In(IList<ChatMessage> messages)
    {
        if (ForwardedViewFit.Largest(messages) is not { } slot) return null;
        var name = messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
            .FirstOrDefault(call => call.CallId == slot.CallId)?.Name ?? "an unnamed tool";
        return new LargestToolResult(name, slot.Text.Length, ToolResultBound.StatedTotal(slot.Text));
    }

    /// <summary>True when this one result accounts for at least half of the estimated tokens.</summary>
    internal bool Dominates(int estimatedTokens) =>
        ForwardedChars / CharsPerToken >= estimatedTokens * DominantShare;

    /// <summary>One sentence naming the tool, the characters forwarded and, when cut, the original total.</summary>
    internal string Describe() =>
        $"The largest tool result in the request came from '{ToolName}': {Number(ForwardedChars)} characters "
        + $"(~{Number(ForwardedChars / CharsPerToken)} tokens) forwarded"
        + (OriginalTotal is { } total ? $", cut from {Number(total)} characters. " : ". ");

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
