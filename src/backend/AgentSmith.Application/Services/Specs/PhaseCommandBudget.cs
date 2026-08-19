using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0470: how much of a phase's command record is kept, and what gives way first.
/// <para>
/// p0469 made the log reach the account at all, and the cap then bound: forty entries with
/// the oldest dropped first, against a live migration phase that issued 157 run_command
/// calls. The searches that prove an absence run early, which is exactly what oldest-first
/// eviction discards — and a trimmed list reads exactly like a complete one, so a missing
/// command reads as a command that never ran. Coverage is most of what the account is asked
/// to judge, so that silence decides phases.
/// </para>
/// <para>
/// Hence: the budget is characters, not entries; an entry's OUTPUT gives way before the
/// entry does, oldest output first; a whole entry is dropped only when nothing is left to
/// shorten; and whenever anything gave way, the list says so at its head.
/// </para>
/// </summary>
internal static class PhaseCommandBudget
{
    /// <summary>What the whole command list may weigh in the account's prompt — enough that a
    /// phase of a couple of hundred commands still shows every one of them by name, and the
    /// diff stays the bulk of the prompt. The retired forty-entry cap bought about half this
    /// at its worst case and paid for it by hiding commands entirely.</summary>
    internal const int MaxChars = 30_000;

    /// <summary>Enough of one command's output to show what it found, and no more: the
    /// account reads dozens of these and the diff is the bulk of its prompt already. This is
    /// the ceiling the whole-list budget may shrink below and can never raise — keeping full
    /// outputs so they could be re-trimmed later would hold a phase's worth of megabyte
    /// buffers in process.</summary>
    internal const int TailChars = 400;

    /// <summary>run_command takes arbitrary shell, and a heredoc patch is tens of kilobytes
    /// in a single command. The entry count bounded that by accident; this bounds it on
    /// purpose.</summary>
    internal const int CommandChars = 200;

    // What a rendered line costs beyond the repo, the command and the tail: the surrounding
    // words, the exit status and, at its longest, the trimmed-output wording. An upper bound,
    // so what the account is handed never weighs more than the budget allows.
    private const int LineOverhead = 80;

    /// <summary>The command as stored: enough to recognise what ran, never a whole patch.</summary>
    internal static string Capped(string command) =>
        command.Length <= CommandChars ? command : command[..CommandChars] + "…";

    /// <summary>Shrinks what is already stored until it fits — the oldest output first, and a
    /// whole entry only once no output is left to give.</summary>
    internal static void Fit(List<PhaseCommandEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var total = entries.Sum(Cost);
        while (total > MaxChars && entries.Count > 0)
        {
            var oldest = entries.FindIndex(e => e.Tail.Length > 0);
            if (oldest < 0)
            {
                total -= Cost(entries[0]);
                entries.RemoveAt(0);
                continue;
            }
            total -= entries[oldest].Tail.Length;
            entries[oldest] = entries[oldest] with { Tail = string.Empty, OutputTrimmed = true };
        }
    }

    /// <summary>The line that leads the evidence when anything gave way, and null when the
    /// record is whole — a notice on a complete list would teach the reader to discount a
    /// complete one. It carries no colon before its first words on purpose: the citation
    /// check reads a leading "name:" as a pipeline step, and a notice is not evidence to be
    /// cited for anything.</summary>
    internal static string? Notice(IReadOnlyList<PhaseCommandEntry> entries, int ran)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var whole = entries.Count(e => !e.OutputTrimmed);
        if (ran <= entries.Count && whole == entries.Count) return null;
        return "not every command the agent ran is shown in full here — it ran "
            + $"{ran} commands in this phase, {entries.Count} are listed and {whole} of those "
            + "carry their output. A command that is missing here was still run.";
    }

    /// <summary>The end of an output is where its verdict is — a grep's last matches, a
    /// build's final error, the summary line of a test run.</summary>
    internal static string Tail(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return string.Empty;
        var text = output.Trim();
        return text.Length <= TailChars ? Collapse(text) : "…" + Collapse(text[^TailChars..]);
    }

    private static string Collapse(string text)
    {
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

    private static int Cost(PhaseCommandEntry entry) =>
        entry.Repo.Length + entry.Command.Length + entry.Tail.Length + LineOverhead;
}
