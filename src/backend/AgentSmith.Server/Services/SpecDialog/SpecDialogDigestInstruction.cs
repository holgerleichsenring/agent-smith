using System.Text;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// What the model is told when it folds the older turns of a long conversation into a digest, and how the digest
/// turn is headed and signed. Prompt content, kept apart from <see cref="SpecDialogDigestWriter"/>, which makes the call.
/// </summary>
internal static class SpecDialogDigestInstruction
{
    /// <summary>The name a digest turn is shown under: the system's text, not a person's.</summary>
    public const string Author = "agent-smith";

    public const string Text =
        "You are condensing the older part of a design conversation so it can be carried forward in place of the "
        + "turns themselves. Later turns of the conversation will be read with your digest and nothing else from "
        + "before them.\n"
        + "Keep, as short plain markdown:\n"
        + "- every decision and every correction the operator made, and WHO made each, by the name on the turn;\n"
        + "- the facts that were settled, with their file:line where one was given;\n"
        + "- the questions still open;\n"
        + "- what was proposed, approved or filed, by its key or title.\n"
        + "Drop greetings, restatements and exploration that led nowhere. Write in the language the conversation "
        + "is held in. Never write a fenced yaml or outcome block: the digest proposes nothing.";

    /// <summary>The line a digest turn opens with, naming the turns (one-based) it summarises.</summary>
    public static string Heading(int from, int through) => from == 0
        ? $"**Digest of turns 1 to {through + 1}** — every turn is still stored above."
        : $"**Digest of turns 1 to {through + 1}** (folding turns {from + 1} to {through + 1} into the digest before "
          + "it) — every turn is still stored above.";

    /// <summary>The text the digest is condensed from: the digest it carries forward, then the folded turns.</summary>
    public static string Exchange(string? previous, IReadOnlyList<SpecDialogTurn> folded)
    {
        var sb = new StringBuilder();
        if (previous is not null) sb.AppendLine("The digest so far:").AppendLine(previous).AppendLine();
        sb.AppendLine("The turns to fold in, oldest first:");
        foreach (var turn in folded) sb.AppendLine(TranscriptWindow.Line(turn));
        return sb.ToString().TrimEnd();
    }
}
