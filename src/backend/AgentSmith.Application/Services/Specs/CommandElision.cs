using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0481: how one over-long command is shortened for the account to read.
/// <para>
/// Cutting a command at its END loses the paths it ran against, and reach is most of what an
/// absence criterion is judged on. Run 0944 refused a Wolverine local-queue search the agent
/// really had run, in both repositories, because the search's pattern sat inside the cap and
/// the PATHS it searched sat past it. 21 of that phase's 106 commands were over the cap.
/// </para>
/// <para>
/// The same cut also landed inside a shell literal, so the rendered line carried an
/// unbalanced quote, the account closed it, and a citation one character longer than the
/// command is not a prefix of it. So the MIDDLE gives way instead: the head carries the tool
/// and the flags that NARROW a search, the tail carries the paths that give it reach, and
/// what a grep loses is the part it can spare.
/// </para>
/// </summary>
internal static class CommandElision
{
    /// <summary>How much of the shortened command is head. Chosen against the citation floor
    /// rather than for roundness: an account that copies only the visible head cites this
    /// share of what it reads, and <see cref="CitationMatch"/> accepts nothing under 60
    /// percent. At the 60 the first draft implied, the run turns on a rounding error.
    /// </summary>
    private const int HeadPercent = 65;

    private const string Marker = "…";

    /// <summary>The command as the account reads it: one line, and never longer than the cap.
    /// </summary>
    internal static string Shorten(string command, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(command);
        var text = Collapse(command);
        if (text.Length <= maxChars) return text;
        var head = maxChars * HeadPercent / 100;
        return text[..HeadEnd(text, head)] + Marker + text[TailStart(text, maxChars - head - 1)..];
    }

    /// <summary>Whether what the account reads is only PART of the command. A citation of a
    /// shortened command has to reproduce the whole of what is shown, because its head alone
    /// does not tell two sibling commands apart.</summary>
    internal static bool WasShortened(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Contains(Marker, StringComparison.Ordinal);
    }

    /// <summary>Runs of whitespace become one space, so a command that spans several lines
    /// stays ONE entry of a list the prompt renders one item per line — a multi-line command
    /// reads there as several commands, and the reader counts what it can see.</summary>
    internal static string Collapse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sb = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            var blank = char.IsWhiteSpace(c);
            if (blank && space) continue;
            sb.Append(blank ? ' ' : c);
            space = blank;
        }
        return sb.ToString().Trim();
    }

    /// <summary>Never between the halves of a surrogate pair: an emoji or a CJK extension in a
    /// path would otherwise be cut into a lone surrogate, which no longer names the file it
    /// came from.</summary>
    private static int HeadEnd(string text, int head) =>
        char.IsHighSurrogate(text[head - 1]) ? head - 1 : head;

    /// <summary>The tail begins at a whole argument. Starting it mid-token would print half a
    /// path as though it were a path — evidence that is partly true, which is the failure this
    /// phase exists to end. The HEAD is cut hard by contrast: a prefix reads as a prefix, and
    /// snapping it backwards could drop the citable share under the floor it was sized for.
    /// </summary>
    private static int TailStart(string text, int tail)
    {
        var start = text.Length - tail;
        var boundary = text.IndexOf(' ', start);
        if (boundary >= 0 && boundary < text.Length - 1) start = boundary + 1;
        return char.IsLowSurrogate(text[start]) ? start + 1 : start;
    }
}
