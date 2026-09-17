using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// A conversation's title, read from its transcript rather than stored: the first line of
/// prose in the first thing the person wrote. Commands never reach the transcript, and the
/// approval answers and edit notes that do can never come first, so the first operator turn
/// is the design message. A pasted draft or log opening that message is skipped as a whole
/// fence, because its first line would name a file format rather than the conversation.
/// </summary>
internal static class SpecDialogConversationTitle
{
    private const int MaxLength = 120;
    private const string Fence = "```";

    internal static string? Of(IReadOnlyList<TranscriptTurn> transcript) =>
        transcript.FirstOrDefault(turn => turn.Role == TranscriptRole.User) is { } first
            ? FirstProseLine(first.Text)
            : null;

    private static string? FirstProseLine(string text)
    {
        var inFence = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith(Fence, StringComparison.Ordinal)) inFence = !inFence;
            else if (!inFence && line.Length > 0) return Shortened(line);
        }
        return null;
    }

    private static string Shortened(string line) =>
        line.Length <= MaxLength ? line : line[..(MaxLength - 1)].TrimEnd() + "…";
}
