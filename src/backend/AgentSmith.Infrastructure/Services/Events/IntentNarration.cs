using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// p0222: the coding-agent-master prompt requires a one-sentence intent before every tool
/// call ("Reading Program.cs to confirm ..."). This takes the first line / sentence of the
/// assistant text and caps it to one activity row, so the tool events of that turn can say
/// WHY they ran.
/// <para>p0423: extracted from EventPublishingChatClient — reading a narration out of a
/// response is not the same job as announcing the call.</para>
/// </summary>
internal static class IntentNarration
{
    private const int Cap = 160;

    public static string? Extract(ChatResponse response)
    {
        var text = response.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        var firstLine = text.Split('\n', 2)[0].Trim();
        var stop = firstLine.IndexOf(". ", StringComparison.Ordinal);
        if (stop > 0) firstLine = firstLine[..(stop + 1)];
        return firstLine.Length > Cap ? firstLine[..Cap] : firstLine;
    }
}
