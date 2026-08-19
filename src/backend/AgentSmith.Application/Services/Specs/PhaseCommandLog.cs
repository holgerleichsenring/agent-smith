using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0452: the commands the agent itself ran in this phase, as evidence the account can read.
/// <para>
/// The delivery account judges from the branch diff and a list of commands — and that list
/// held only the stages VERIFICATION ran, never the agent's own. Three live runs died on
/// that blindness with the same verdict: 459d "no listed command ran the required search",
/// 587c "covers the Server repository only", 929f "records a planned scan but provides no
/// completed scan". In 929f the agent had run nineteen searching commands across both
/// repositories, two of them printing a labelled legacy-reference report. None was visible
/// to the reader asked to judge whether a search happened.
/// </para>
/// <para>
/// This does not decide anything. A wrongly scoped search is still weak evidence and the
/// account must still say so — the point is that it can see it at all.
/// </para>
/// </summary>
public sealed class PhaseCommandLog
{
    /// <summary>Enough of the output to show what a command found, and no more: the account
    /// reads dozens of these and the diff is the bulk of its prompt already.</summary>
    internal const int TailChars = 400;

    /// <summary>A phase runs hundreds of tool calls; the account needs the shape, not a log
    /// file. Oldest are dropped — the last commands of a phase are the ones that prove it.</summary>
    internal const int MaxEntries = 40;

    private readonly List<Entry> _entries = [];
    private readonly Lock _sync = new();

    /// <summary>Runs the command and records it — the call site stays one line, and no
    /// caller has to remember to log.</summary>
    public async Task<string> RecordAsync(string? repo, string command, Func<Task<string>> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var output = await run();
        Record(repo, command, output);
        return output;
    }

    public void Record(string? repo, string command, string? output)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        lock (_sync)
        {
            _entries.Add(new Entry(repo ?? string.Empty, command.Trim(), Tail(output)));
            if (_entries.Count > MaxEntries) _entries.RemoveAt(0);
        }
    }

    /// <summary>One line per command, in the order they ran, for the account's prompt.</summary>
    public IReadOnlyList<string> Evidence()
    {
        lock (_sync)
            return [.. _entries.Select(e =>
                (e.Repo.Length > 0 ? $"{e.Repo}: " : string.Empty)
                + $"the agent ran '{e.Command}'"
                + (e.Tail.Length > 0 ? $" — output: {e.Tail}" : " — no output"))];
    }

    private static string Tail(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return string.Empty;
        var text = output.Trim();
        // The END of a command's output is where its verdict is — a grep's last matches, a
        // build's final error, the summary line of a test run.
        return text.Length <= TailChars
            ? Collapse(text)
            : "…" + Collapse(text[^TailChars..]);
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

    private readonly record struct Entry(string Repo, string Command, string Tail);
}
