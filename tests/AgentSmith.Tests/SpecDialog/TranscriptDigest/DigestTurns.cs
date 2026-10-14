using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Tests.SpecDialog.TranscriptDigest;

/// <summary>Turns for the transcript digest's tests: an operator's, an assistant's, a digest, and filler of a size.</summary>
internal static class DigestTurns
{
    public static SpecDialogTurn U(string text, string? author = null) =>
        new(SpecDialogTurn.UserRole, text, Author: author);

    public static SpecDialogTurn A(string text, SpecDialogTurnKind kind = SpecDialogTurnKind.Answer) =>
        new(SpecDialogTurn.AssistantRole, text, kind);

    public static SpecDialogTurn Digest(string text, int through) =>
        new(SpecDialogTurn.AssistantRole, text, SpecDialogTurnKind.Digest, "agent-smith", through);

    /// <summary>A text of exactly <paramref name="chars"/> characters that starts with <paramref name="marker"/>.</summary>
    public static string Big(string marker, int chars) => marker + new string('x', chars - marker.Length);

    /// <summary>The rendered width of turns, as the window measures them.</summary>
    public static int Width(IEnumerable<SpecDialogTurn> turns) =>
        turns.Sum(t => TranscriptWindow.Line(t).Length + Environment.NewLine.Length);
}
